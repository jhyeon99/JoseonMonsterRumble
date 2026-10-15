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
    private const float DefaultDuration = 0.45f;
    private const string GlowShaderName = "Team06/ProjectileGlow";
    private const float PotionSampleRate = 16f;
    private const int BurstParticleCount = 16;
    private static readonly int PotionStateHash = Animator.StringToHash("Player_Heal");
    private enum AuraPhase { Idle, WaitingForSip, Active }

    public event Action OnRecoveryPulse;

    [Header("곰방대 회복 타이밍")]
    [SerializeField, Min(0)] private int _sipFrame = 3;

    [Header("시각 연출 설정")]
    [SerializeField] private Color _auraColor = new Color(0.2f, 0.95f, 0.45f, 1.0f);
    [SerializeField, Min(1.0f)] private float _minBrightness = 1.15f;
    [SerializeField, Min(1.0f)] private float _maxBrightness = 1.45f;
    [SerializeField] private Material _glowMaterial;
    [SerializeField] private Material _particleMaterial;
    [SerializeField] private SpriteRenderer _sourceRenderer;
    [SerializeField] private ParticleSystem _auraParticles;

    [Header("상태 모니터링")]
    [SerializeField, ReadOnly] private AuraPhase _phase;
    [SerializeField, ReadOnly] private float _remainingDuration;

    private MaterialPropertyBlock _propBlock;
    private Material _originalMaterial;
    private Material _ownedGlowMaterial;
    private Material _ownedParticleMaterial;
    private PlayerWeaponBuff _weaponBuff;
    private Player _player;
    private Animator _animator;
    private float _sipElapsed;
    private float _pulseDuration;

    public bool IsAuraActive => _phase == AuraPhase.Active;
    public bool IsWaitingForSip => _phase == AuraPhase.WaitingForSip;

    private void Awake()
    {
        _weaponBuff = GetComponent<PlayerWeaponBuff>();
        _player = GetComponent<Player>();
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
#pragma warning disable IDE0007 // 프로젝트 규칙: 메서드 반환값은 명시적 형식으로 선언한다.
            Shader glowShader = Shader.Find(GlowShaderName);
#pragma warning restore IDE0007
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
        StopAura();
    }

    private void OnDestroy()
    {
        StopAura();
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
        return isActiveAndEnabled && IsAuraActive && _sourceRenderer == renderer && _glowMaterial != null;
    }

    public void PlayAura(float duration = DefaultDuration)
    {
        if (!isActiveAndEnabled) return;

        if (_weaponBuff == null)
        {
            _weaponBuff = GetComponent<PlayerWeaponBuff>();
        }

        StopAura();
        _pulseDuration = Mathf.Max(0.1f, duration);
        _sipElapsed = 0f;
        _animator = _player != null ? _player.Animator : null;
        _phase = AuraPhase.WaitingForSip;
    }

    public void PlayRecoveryPulse(float duration = DefaultDuration)
    {
        if (!isActiveAndEnabled) return;

        StopAura();
        _pulseDuration = Mathf.Max(0.1f, duration);
        StartRecoveryPulse();
    }

    private void StartRecoveryPulse()
    {
        _phase = AuraPhase.Active;
        _sipElapsed = 0f;
        _remainingDuration = _pulseDuration;
        ApplyRendererVisuals();

        if (_auraParticles != null)
        {
            if (!_auraParticles.isPlaying)
            {
                _auraParticles.Play();
            }
            _auraParticles.Emit(BurstParticleCount);
            _auraParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        OnRecoveryPulse?.Invoke();
    }

    public void StopAura()
    {
        bool wasActive = IsAuraActive;
        _phase = AuraPhase.Idle;
        _sipElapsed = 0f;
        _pulseDuration = 0f;
        _remainingDuration = 0f;

        if (wasActive)
        {
            ResetRendererVisuals();
        }

        if (_auraParticles != null)
        {
            _auraParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void Update()
    {
        if (_phase == AuraPhase.WaitingForSip)
        {
            if (Time.deltaTime <= 0f)
            {
                return;
            }
            if (_player != null && _player.Health.IsDead)
            {
                StopAura();
                return;
            }
            _sipElapsed += Time.deltaTime;
            float sipTime = _sipFrame / PotionSampleRate;
            bool isInterrupted = _player != null && _player.StateMachine.CurrentState != _player.PotionState;
            AnimatorStateInfo state = _animator != null ? _animator.GetCurrentAnimatorStateInfo(0) : default;
            bool isSipReached = state.shortNameHash == PotionStateHash
                ? state.normalizedTime * state.length >= sipTime : _sipElapsed >= sipTime;
            if (isSipReached || isInterrupted)
            {
                // 실제 회복은 이미 적용됐으므로 동작이 중단돼도 결과 표시는 누락하지 않는다.
                StartRecoveryPulse();
            }
            return;
        }
        if (!IsAuraActive)
        {
            return;
        }

        _remainingDuration -= Time.deltaTime;

        if (_remainingDuration <= 0f)
        {
            StopAura();
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

        if (_sourceRenderer.sharedMaterial != _glowMaterial)
        {
            _sourceRenderer.sharedMaterial = _glowMaterial;
        }

        // 한 번 강하게 나타난 뒤 감쇠한다. 반복 점멸하지 않는다.
        float pulse = Mathf.Clamp01(_remainingDuration / Mathf.Max(0.1f, _pulseDuration));
        pulse *= pulse;
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

        if (_weaponBuff != null && _weaponBuff.HasActiveVisuals(_sourceRenderer))
        {
            _weaponBuff.ApplyRendererVisuals();
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
        auraObject.transform.localPosition = new Vector3(0f, 0.45f, 0f);

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
                _ownedParticleMaterial.color = new Color(0.2f, 0.95f, 0.45f, 0.9f);
                psRenderer.sharedMaterial = _ownedParticleMaterial;
            }
        }

        ParticleSystem.MainModule main = _auraParticles.main;
        main.playOnAwake = false;
        main.duration = 1.0f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.75f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.4f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 50;

        ParticleSystem.EmissionModule emission = _auraParticles.emission;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 0f;

        ParticleSystem.ShapeModule shape = _auraParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.32f;

        ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime = _auraParticles.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0f, 0f);

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
    }
}
