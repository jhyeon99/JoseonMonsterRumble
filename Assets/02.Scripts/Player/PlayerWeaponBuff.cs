using System;
using UnityEngine;

/// <summary>
/// 회피 성공 시 무기와 본체에 푸른 영기를 부여하고 일반 근접 공격력을 증가시키는 버프 컴포넌트.
/// </summary>
[DisallowMultipleComponent]
public class PlayerWeaponBuff : MonoBehaviour
{
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
    private static readonly int TintStrengthId = Shader.PropertyToID("_TintStrength");
    private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");

    private const string MissingRendererWarning = "[PlayerWeaponBuff] SpriteRenderer 참조를 찾을 수 없습니다.";
    private const string AuraObjectName = "WeaponBuffAura";
    private const float MinimumDuration = 0.05f;
    private const float DefaultDuration = 4.0f;
    private const float DefaultMultiplier = 1.30f;
    private const string GlowShaderName = "Team06/ProjectileGlow";

    public event Action OnBuffActivated;
    public event Action OnBuffDeactivated;
    public event Action<float, float> OnBuffTimerUpdated; // remaining, total

    [Header("버프 수치 설정")]
    [SerializeField, Min(0.1f)] private float _duration = DefaultDuration;
    [SerializeField, Min(1.0f)] private float _damageMultiplier = DefaultMultiplier;

    [Header("시각 연출 설정")]
    [SerializeField] private Color _glowColor = new Color(0.25f, 0.72f, 1.0f, 1.0f);
    [SerializeField, Min(1.0f)] private float _minBrightness = 1.15f;
    [SerializeField, Min(1.0f)] private float _maxBrightness = 1.45f;
    [SerializeField, Min(0.1f)] private float _pulseSpeed = 5.5f;
    [SerializeField] private Material _glowMaterial;
    [SerializeField] private Material _particleMaterial;
    [SerializeField] private SpriteRenderer _sourceRenderer;
    [SerializeField] private ParticleSystem _auraParticles;

    [Header("상태 모니터링")]
    [SerializeField, ReadOnly] private bool _isBuffActive;
    [SerializeField, ReadOnly] private float _remainingDuration;

    private MaterialPropertyBlock _propBlock;
    private Material _originalMaterial;
    private Material _ownedGlowMaterial;
    private Material _ownedParticleMaterial;
    private PlayerPotionAura _potionAura;

    public bool IsBuffActive => _isBuffActive;
    public float RemainingDuration => _remainingDuration;
    public float Duration => _duration;
    public float DamageMultiplier => _isBuffActive ? _damageMultiplier : 1.0f;

    private void Awake()
    {
        _potionAura = GetComponent<PlayerPotionAura>();
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
            var glowShader = Shader.Find(GlowShaderName);
            if (glowShader != null)
            {
                _ownedGlowMaterial = new Material(glowShader);
                _glowMaterial = _ownedGlowMaterial;
            }
        }

        InitializeParticleSystem();
    }

    private void OnDisable()
    {
        DeactivateBuff();
    }

    private void OnDestroy()
    {
        DeactivateBuff();
        if (_ownedGlowMaterial != null)
        {
            Destroy(_ownedGlowMaterial);
        }
        if (_ownedParticleMaterial != null)
        {
            Destroy(_ownedParticleMaterial);
        }
    }

    internal bool HasActiveVisuals(SpriteRenderer renderer)
    {
        return isActiveAndEnabled && _isBuffActive && _sourceRenderer == renderer && _glowMaterial != null;
    }

    public void ActivateBuff(float duration = DefaultDuration, float multiplier = DefaultMultiplier)
    {
        if (!isActiveAndEnabled) return;

        if (_potionAura == null)
        {
            _potionAura = GetComponent<PlayerPotionAura>();
        }

        _duration = Mathf.Max(MinimumDuration, duration);
        _damageMultiplier = Mathf.Max(1.0f, multiplier);
        _remainingDuration = _duration;

        bool wasActive = _isBuffActive;
        _isBuffActive = true;

        ApplyRendererVisuals();

        if (_auraParticles != null)
        {
            if (!_auraParticles.isPlaying)
            {
                _auraParticles.Play();
            }
            _auraParticles.Emit(12);
        }

        if (!wasActive)
        {
            OnBuffActivated?.Invoke();
        }

        OnBuffTimerUpdated?.Invoke(_remainingDuration, _duration);
    }

    public void DeactivateBuff()
    {
        if (!_isBuffActive) return;

        _isBuffActive = false;
        _remainingDuration = 0f;

        ResetRendererVisuals();

        if (_auraParticles != null)
        {
            _auraParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        OnBuffDeactivated?.Invoke();
        OnBuffTimerUpdated?.Invoke(0f, _duration);
    }

    private void Update()
    {
        if (!_isBuffActive) return;

        _remainingDuration -= Time.deltaTime;
        OnBuffTimerUpdated?.Invoke(Mathf.Max(0f, _remainingDuration), _duration);

        if (_remainingDuration <= 0f)
        {
            DeactivateBuff();
        }
    }

    private void LateUpdate()
    {
        ApplyRendererVisuals();
    }

    internal void ApplyRendererVisuals()
    {
        if (!HasActiveVisuals(_sourceRenderer) || _sourceRenderer == null || _propBlock == null)
        {
            return;
        }
        // 물약 연출 동안 공격력 버프는 유지하되 본체 색상을 덮어쓰지 않는다.
        if (_potionAura != null && _potionAura.HasActiveVisuals(_sourceRenderer))
        {
            return;
        }

        if (_sourceRenderer.sharedMaterial != _glowMaterial)
        {
            _sourceRenderer.sharedMaterial = _glowMaterial;
        }

        float pulse = Mathf.Sin(Time.time * _pulseSpeed) * 0.5f + 0.5f;
        float brightness = Mathf.Lerp(_minBrightness, _maxBrightness, pulse);
        float tintStrength = Mathf.Lerp(0.22f, 0.38f, pulse);

        // 버프 종료 직전 0.6초 구간에서 은은한 감쇠 페이드아웃
        if (_remainingDuration < 0.6f)
        {
            float fadeRatio = Mathf.Clamp01(_remainingDuration / 0.6f);
            brightness = Mathf.Lerp(1.0f, brightness, fadeRatio);
            tintStrength *= fadeRatio;
        }

        _sourceRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetFloat(BrightnessId, brightness);
        _propBlock.SetFloat(TintStrengthId, tintStrength);
        _propBlock.SetColor(TintColorId, _glowColor);
        _sourceRenderer.SetPropertyBlock(_propBlock);
    }

    private void ResetRendererVisuals()
    {
        if (_sourceRenderer == null) return;

        if (_potionAura != null && _potionAura.HasActiveVisuals(_sourceRenderer))
        {
            _potionAura.ApplyRendererVisuals();
            return;
        }

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
        auraObject.transform.localPosition = new Vector3(0.12f, 0.45f, 0f);

        _auraParticles = auraObject.AddComponent<ParticleSystem>();
        _auraParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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
                _ownedParticleMaterial = new Material(particleShader);
                _ownedParticleMaterial.color = new Color(0.4f, 0.85f, 1.0f, 0.9f);
                psRenderer.sharedMaterial = _ownedParticleMaterial;
            }
        }

        // 파티클 시스템 메인 모듈 구성
        ParticleSystem.MainModule main = _auraParticles.main;
        main.duration = 1.0f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.36f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 50;

        // 방출 모듈 (기본 방출 + 이동 시 자연스러운 잔류 영기 궤적 방출)
        ParticleSystem.EmissionModule emission = _auraParticles.emission;
        emission.rateOverTime = 14f;
        emission.rateOverDistance = 7f;

        // 방출 형태 모듈 (플레이어 상체 중심 원형 영역)
        ParticleSystem.ShapeModule shape = _auraParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.28f;

        // 수명에 따른 상승 기류 속도 모듈 (세 축의 커브 모드를 동일하게 맞춰 엔진 경고 방지)
        ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime = _auraParticles.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // 유기적 일렁임을 위한 노이즈 모듈
        ParticleSystem.NoiseModule noise = _auraParticles.noise;
        noise.enabled = true;
        noise.frequency = 1.6f;
        noise.strength = 0.4f;
        noise.damping = true;

        // 신령한 도깨비불 색상 그라디언트 (백청색 -> 청록/하늘색 -> 짙은 청색 소멸)
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = _auraParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.85f, 0.96f, 1.0f), 0.0f),
                new GradientColorKey(new Color(0.25f, 0.75f, 1.0f), 0.3f),
                new GradientColorKey(new Color(0.08f, 0.35f, 0.92f), 0.75f),
                new GradientColorKey(new Color(0.02f, 0.12f, 0.5f), 1.0f)
            },
            new[]
            {
                new GradientAlphaKey(0.0f, 0.0f),
                new GradientAlphaKey(0.85f, 0.15f),
                new GradientAlphaKey(0.65f, 0.7f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        colorOverLifetime.color = gradient;

        // 수명에 따른 크기 모듈 (작게 생성 -> 팽창 -> 서서히 소멸)
        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = _auraParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var sizeCurve = new AnimationCurve(
            new Keyframe(0.0f, 0.35f),
            new Keyframe(0.25f, 1.0f),
            new Keyframe(1.0f, 0.0f)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);
    }
}
