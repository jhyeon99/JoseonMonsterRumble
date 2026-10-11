using System;
using UnityEngine;

/// <summary>
/// 물약 복용 시 플레이어 본체에 에메랄드 비취색 펄스 발광을 부여하고
/// 초록빛 소울 파티클 영기를 분출시키는 연출 컴포넌트.
/// </summary>
[DisallowMultipleComponent]
public class PlayerPotionAura : MonoBehaviour
{
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
    private static readonly int TintStrengthId = Shader.PropertyToID("_TintStrength");
    private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");

    private const string MissingRendererWarning = "[PlayerPotionAura] SpriteRenderer 참조를 찾을 수 없습니다.";
    private const string AuraObjectName = "PotionAura";
    private const float DefaultDuration = 1.4f;

    [Header("시각 연출 설정")]
    [SerializeField] private Color _auraColor = new Color(0.2f, 0.95f, 0.45f, 1.0f);
    [SerializeField, Min(1.0f)] private float _minBrightness = 1.15f;
    [SerializeField, Min(1.0f)] private float _maxBrightness = 1.45f;
    [SerializeField, Min(0.1f)] private float _pulseSpeed = 6.0f;
    [SerializeField] private Material _glowMaterial;
    [SerializeField] private Material _particleMaterial;
    [SerializeField] private SpriteRenderer _sourceRenderer;
    [SerializeField] private ParticleSystem _auraParticles;

    [Header("상태 모니터링")]
    [SerializeField, ReadOnly] private bool _isAuraActive;
    [SerializeField, ReadOnly] private float _remainingDuration;

    private MaterialPropertyBlock _propBlock;
    private Material _originalMaterial;

    public bool IsAuraActive => _isAuraActive;

    private void Awake()
    {
        if (_sourceRenderer == null)
        {
            _sourceRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (_sourceRenderer != null)
        {
            _originalMaterial = _sourceRenderer.sharedMaterial;
        }
        else
        {
            Debug.LogWarning(MissingRendererWarning, this);
        }

        _propBlock = new MaterialPropertyBlock();

        if (_glowMaterial == null)
        {
            var glowShader = Shader.Find("Team06/ProjectileGlow");
            if (glowShader != null)
            {
                _glowMaterial = new Material(glowShader);
            }
        }

        InitializeParticleSystem();
    }

    private void OnDisable()
    {
        StopAura();
    }

    public void PlayAura(float duration = DefaultDuration)
    {
        if (!isActiveAndEnabled) return;

        _remainingDuration = Mathf.Max(0.1f, duration);
        _isAuraActive = true;

        if (_sourceRenderer != null && _glowMaterial != null)
        {
            _sourceRenderer.sharedMaterial = _glowMaterial;
        }

        if (_auraParticles != null)
        {
            if (!_auraParticles.isPlaying)
            {
                _auraParticles.Play();
            }
            _auraParticles.Emit(16);
        }
    }

    public void StopAura()
    {
        if (!_isAuraActive) return;

        _isAuraActive = false;
        _remainingDuration = 0f;

        ResetRendererVisuals();

        if (_auraParticles != null)
        {
            _auraParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void Update()
    {
        if (!_isAuraActive) return;

        _remainingDuration -= Time.deltaTime;

        if (_remainingDuration <= 0f)
        {
            StopAura();
        }
    }

    private void LateUpdate()
    {
        if (!_isAuraActive || _sourceRenderer == null || _propBlock == null) return;

        float pulse = Mathf.Sin(Time.time * _pulseSpeed) * 0.5f + 0.5f;
        float brightness = Mathf.Lerp(_minBrightness, _maxBrightness, pulse);
        float tintStrength = Mathf.Lerp(0.25f, 0.45f, pulse);

        // 버프 종료 직전 0.4초 구간에서 은은한 감쇠 페이드아웃
        if (_remainingDuration < 0.4f)
        {
            float fadeRatio = Mathf.Clamp01(_remainingDuration / 0.4f);
            brightness = Mathf.Lerp(1.0f, brightness, fadeRatio);
            tintStrength *= fadeRatio;
        }

        _sourceRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetFloat(BrightnessId, brightness);
        _propBlock.SetFloat(TintStrengthId, tintStrength);
        _propBlock.SetColor(TintColorId, _auraColor);
        _sourceRenderer.SetPropertyBlock(_propBlock);
    }

    private void ResetRendererVisuals()
    {
        if (_sourceRenderer == null) return;

        if (_propBlock != null)
        {
            _sourceRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetFloat(BrightnessId, 1.0f);
            _propBlock.SetFloat(TintStrengthId, 0f);
            _sourceRenderer.SetPropertyBlock(_propBlock);
        }

        if (_originalMaterial != null)
        {
            _sourceRenderer.sharedMaterial = _originalMaterial;
        }
    }

    private void InitializeParticleSystem()
    {
        if (_auraParticles != null) return;

        Transform existingAura = transform.Find(AuraObjectName);
        if (existingAura != null && existingAura.TryGetComponent(out _auraParticles))
        {
            return;
        }

        var auraObject = new GameObject(AuraObjectName);
        auraObject.transform.SetParent(transform, false);
        auraObject.transform.localPosition = new Vector3(0f, 0.45f, 0f);

        _auraParticles = auraObject.AddComponent<ParticleSystem>();
        ParticleSystemRenderer psRenderer = auraObject.GetComponent<ParticleSystemRenderer>();

        if (_sourceRenderer != null)
        {
            psRenderer.sortingLayerID = _sourceRenderer.sortingLayerID;
            psRenderer.sortingOrder = _sourceRenderer.sortingOrder + 1;
        }

        Material particleMat = _particleMaterial;
        if (particleMat == null)
        {
            particleMat = Resources.Load<Material>("MT_SoulParticle");
        }

        if (particleMat != null)
        {
            psRenderer.sharedMaterial = particleMat;
        }
        else
        {
            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Sprites/Default");

            if (particleShader != null)
            {
                var fallbackMat = new Material(particleShader);
                fallbackMat.color = new Color(0.2f, 0.95f, 0.45f, 0.9f);
                psRenderer.sharedMaterial = fallbackMat;
            }
        }

        ParticleSystem.MainModule main = _auraParticles.main;
        main.duration = 1.0f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.75f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.4f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 50;

        ParticleSystem.EmissionModule emission = _auraParticles.emission;
        emission.rateOverTime = 18f;
        emission.rateOverDistance = 8f;

        ParticleSystem.ShapeModule shape = _auraParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.32f;

        ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime = _auraParticles.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);

        ParticleSystem.NoiseModule noise = _auraParticles.noise;
        noise.enabled = true;
        noise.frequency = 1.8f;
        noise.strength = 0.45f;
        noise.damping = true;

        // 에메랄드 초록빛 영기 그라디언트 (연초록빛 백색 -> 에메랄드 비취색 -> 짙은 쑥빛 소멸)
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = _auraParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.85f, 1.0f, 0.88f), 0.0f),
                new GradientColorKey(new Color(0.22f, 0.95f, 0.48f), 0.3f),
                new GradientColorKey(new Color(0.08f, 0.72f, 0.32f), 0.75f),
                new GradientColorKey(new Color(0.02f, 0.45f, 0.16f), 1.0f)
            },
            new[]
            {
                new GradientAlphaKey(0.0f, 0.0f),
                new GradientAlphaKey(0.9f, 0.15f),
                new GradientAlphaKey(0.7f, 0.7f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        colorOverLifetime.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = _auraParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var sizeCurve = new AnimationCurve(
            new Keyframe(0.0f, 0.3f),
            new Keyframe(0.3f, 1.0f),
            new Keyframe(1.0f, 0.0f)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

        _auraParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
