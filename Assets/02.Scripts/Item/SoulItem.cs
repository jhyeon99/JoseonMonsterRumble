using System.Collections;
using UnityEngine;

public class SoulItem : Item
{
    public static event System.Action<SoulItem> OnCollected;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCollectionListeners()
    {
        OnCollected = null;
    }
    private const float HeadFlameIntensity = 1f;
    private const float HeadCoreThreshold = 0.93f;
    private const float HeadCoreSoftness = 0.025f;
    private const float ScatterDrag = 5f;
    private const float DeathScatterDrag = 3f;
    private const float MinimumScatterSpeedSquared = 0.01f;
    private const float MotionSeedRange = 1000f;
    private const float MinimumMotionFrequency = 2f;
    private const float MaximumMotionFrequency = 4f;
    private const float MinimumTurnStrength = 6f;
    private const float MaximumTurnStrength = 10f;
    private const float MinimumDriftAmplitude = 0.15f;
    private const float MaximumDriftAmplitude = 0.35f;
    private const float MinimumAbsorbDurationMultiplier = 0.8f;
    private const float MaximumAbsorbDurationMultiplier = 1.2f;
    private const float NoiseSignedRange = 2f;
    private static readonly int FlameColorId = Shader.PropertyToID("_FlameColor");
    private static readonly int FlameInnerColorId = Shader.PropertyToID("_FlameInnerColor");
    private static readonly int FlameIntensityId = Shader.PropertyToID("_FlameIntensity");
    private static readonly int CoreThresholdId = Shader.PropertyToID("_CoreThreshold");
    private static readonly int CoreSoftnessId = Shader.PropertyToID("_CoreSoftness");

    [SerializeField] private float _soulAmount = 0.2f;

    [SerializeField] private float _waitTime = 0.2f;
    [SerializeField] private float _absorbDuration = 0.35f;
    [SerializeField] private float _arcHeight = 0.5f;
    [SerializeField] private float _randomRange = 0.2f;
    [SerializeField] private Vector3 _targetOffset = new Vector3(0f, 0.7f, 0f);

    [Header("시각 연출 컴포넌트")]
    [SerializeField] private TrailRenderer _trailRenderer;
    [SerializeField] private ParticleSystem _headParticle;
    [SerializeField] private SpriteRenderer _headSprite;
    private MaterialPropertyBlock _headProperties;

    private Transform _playerTransform;
    private bool _isWaiting;
    private bool _isCollected;

    private float _absorbTimer;
    private Vector2 _originPosition;
    private Vector2 _controlPoint;

    private Vector2 _scatterVelocity;
    private float _scatterTimer;
    private float _currentWaitTime;
    private float _currentAbsorbDuration;
    private bool _hasDeathMotion;
    private float _motionSeed;
    private float _motionFrequency;
    private float _turnStrength;
    private float _driftAmplitude;
    private Vector2 _absorbDriftDirection;
    private Vector3 _baseScale = Vector3.one;
    private float _baseTrailTime;
    private float _baseParticleSize = 1f;

    private void Awake()
    {
        InitializeRenderers();
        _baseScale = transform.localScale;
        if (_headSprite != null)
        {
            _headProperties = new MaterialPropertyBlock();
        }
    }

    private void InitializeRenderers()
    {
        if (_trailRenderer == null)
        {
            _trailRenderer = GetComponentInChildren<TrailRenderer>(true);
            if (_trailRenderer != null)
            {
                _baseTrailTime = _trailRenderer.time;
            }
        }
        else if (_baseTrailTime <= 0f)
        {
            _baseTrailTime = _trailRenderer.time;
        }

        if (_headParticle == null)
        {
            _headParticle = GetComponentInChildren<ParticleSystem>(true);
            if (_headParticle != null)
            {
                _baseParticleSize = _headParticle.main.startSizeMultiplier;
            }
        }
        else if (_baseParticleSize <= 0f)
        {
            _baseParticleSize = _headParticle.main.startSizeMultiplier;
        }
    }

    private void Update()
    {
        if (_playerTransform == null || PauseController.IsPaused)
        {
            return;
        }

        if (_isWaiting)
        {
            UpdateScatterMotion();
            return;
        }

        _absorbTimer += Time.deltaTime;
        float bezierTime = Mathf.Clamp01(_absorbTimer / _currentAbsorbDuration);

        Vector3 targetPosition = _playerTransform.position + _targetOffset;
        transform.position = CalculateBezier(_originPosition, _controlPoint, targetPosition, bezierTime);
        if (_hasDeathMotion)
        {
            // The squared envelope keeps both ends of the return path free of lateral jolts.
            float envelope = Mathf.Sin(bezierTime * Mathf.PI);
            float drift = CalculateMotionNoise(_scatterTimer + _absorbTimer) *
                          _driftAmplitude * envelope * envelope;
            transform.position += (Vector3)(_absorbDriftDirection * drift);
        }

        if (bezierTime >= 1f)
        {
            if (_playerTransform.TryGetComponent(out Player player))
            {
                OnCollect(player);
            }
            gameObject.SetActive(false);
        }
    }

    private void UpdateScatterMotion()
    {
        _scatterTimer += Time.deltaTime;
        if (_scatterVelocity.sqrMagnitude <= MinimumScatterSpeedSquared)
        {
            return;
        }

        if (_hasDeathMotion)
        {
            float progress = Mathf.Clamp01(_scatterTimer / _currentWaitTime);
            float angle = CalculateMotionNoise(_scatterTimer) * _turnStrength *
                          Mathf.Sin(progress * Mathf.PI) * Time.deltaTime;
            float sine = Mathf.Sin(angle);
            float cosine = Mathf.Cos(angle);
            _scatterVelocity = new Vector2(
                _scatterVelocity.x * cosine - _scatterVelocity.y * sine,
                _scatterVelocity.x * sine + _scatterVelocity.y * cosine);
        }

        transform.position += (Vector3)(_scatterVelocity * Time.deltaTime);
        float drag = _hasDeathMotion ? DeathScatterDrag : ScatterDrag;
        _scatterVelocity = Vector2.Lerp(_scatterVelocity, Vector2.zero, Time.deltaTime * drag);
    }

    private float CalculateMotionNoise(float elapsedTime)
    {
        return Mathf.PerlinNoise(_motionSeed, elapsedTime * _motionFrequency) * NoiseSignedRange - 1f;
    }

    public void EnableDeathMotion()
    {
        _hasDeathMotion = true;
        _motionSeed = Random.Range(0f, MotionSeedRange);
        _motionFrequency = Random.Range(MinimumMotionFrequency, MaximumMotionFrequency);
        _turnStrength = Random.Range(MinimumTurnStrength, MaximumTurnStrength);
        _driftAmplitude = Random.Range(MinimumDriftAmplitude, MaximumDriftAmplitude);
        _currentAbsorbDuration = _absorbDuration *
                                 Random.Range(MinimumAbsorbDurationMultiplier, MaximumAbsorbDurationMultiplier);
    }

    private void ResetMotion()
    {
        _hasDeathMotion = false;
        _scatterTimer = 0f;
        _currentAbsorbDuration = _absorbDuration;
        _motionSeed = 0f;
        _motionFrequency = 0f;
        _turnStrength = 0f;
        _driftAmplitude = 0f;
        _absorbDriftDirection = Vector2.zero;
    }

    private IEnumerator WaitRoutine(float duration)
    {
        _isWaiting = true;
        yield return new WaitForSeconds(duration);
        _isWaiting = false;

        _originPosition = transform.position;
        _controlPoint = (Vector2)transform.position + Vector2.up * _arcHeight +
                        (Vector2)Random.insideUnitSphere * _randomRange;
        if (_hasDeathMotion && _playerTransform != null)
        {
            Vector2 returnDirection = ((Vector2)(_playerTransform.position + _targetOffset) - _originPosition).normalized;
            _absorbDriftDirection = new Vector2(-returnDirection.y, returnDirection.x);
        }
    }

    private Vector3 CalculateBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        return Vector3.Lerp(Vector3.Lerp(p0, p1, t), Vector3.Lerp(p1, p2, t), t);
    }

    public override void OnSpawn()
    {
        EnsurePlayerTransform();
        _isWaiting = true;
        _isCollected = false;
        _absorbTimer = 0f;
        _currentWaitTime = _waitTime;
        _scatterVelocity = Vector2.zero;
        ResetMotion();

        transform.localScale = _baseScale;
        if (_trailRenderer != null)
        {
            _trailRenderer.widthMultiplier = 1f;
            if (_baseTrailTime > 0f)
            {
                _trailRenderer.time = _baseTrailTime;
            }
        }

        if (_headParticle != null)
        {
            ParticleSystem.MainModule main = _headParticle.main;
            main.startSizeMultiplier = _baseParticleSize;
        }

        ResetTrail();
        StartCoroutine(WaitRoutine(_currentWaitTime));
    }

    public void Setup(float amount, Color color, float scaleMultiplier, Vector2 initialVelocity, float waitTime = 0.35f)
    {
        InitializeRenderers();
        EnsurePlayerTransform();

        _soulAmount = amount;
        _currentWaitTime = waitTime;
        _scatterVelocity = initialVelocity;
        _absorbTimer = 0f;
        _isWaiting = true;
        _isCollected = false;

        ResetMotion();

        transform.localScale = _baseScale * scaleMultiplier;
        if (_trailRenderer != null)
        {
            _trailRenderer.widthMultiplier = scaleMultiplier;
            if (_baseTrailTime > 0f)
            {
                float durationFactor = Mathf.Lerp(1f, 1.45f, Mathf.InverseLerp(1f, 2f, scaleMultiplier));
                _trailRenderer.time = _baseTrailTime * durationFactor;
            }
        }

        if (_headParticle != null)
        {
            ParticleSystem.MainModule main = _headParticle.main;
            main.startSizeMultiplier = _baseParticleSize * scaleMultiplier;
        }

        ApplyColor(color);
        ResetTrail();

        StopAllCoroutines();
        StartCoroutine(WaitRoutine(_currentWaitTime));
    }

    private void EnsurePlayerTransform()
    {
        if (_playerTransform == null)
        {
            Player player = FindAnyObjectByType<Player>();
            if (player != null)
            {
                _playerTransform = player.transform;
            }
        }
    }

    private void ResetTrail()
    {
        if (_trailRenderer != null)
        {
            _trailRenderer.Clear();
        }
    }

    private void ApplyColor(Color color)
    {
        if (_headSprite != null)
        {
            // Keep the bright core intact; pass the hue to the flame ramp instead of multiplying the sprite.
            _headSprite.color = Color.white;
            _headSprite.GetPropertyBlock(_headProperties);
            _headProperties.SetColor(FlameColorId, color);
            _headProperties.SetColor(FlameInnerColorId, color);
            _headProperties.SetFloat(FlameIntensityId, HeadFlameIntensity);
            _headProperties.SetFloat(CoreThresholdId, HeadCoreThreshold);
            _headProperties.SetFloat(CoreSoftnessId, HeadCoreSoftness);
            _headSprite.SetPropertyBlock(_headProperties);
        }
        if (_trailRenderer != null)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(color, 0f), new GradientColorKey(color * 0.8f, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            _trailRenderer.colorGradient = gradient;
        }

        if (_headParticle != null)
        {
            ParticleSystem.MainModule main = _headParticle.main;
            main.startColor = color;
        }
    }

    public override void OnCollect(Player player)
    {
        if (_isCollected) return;
        _isCollected = true;
        player.Soul.AddSoul(_soulAmount);
        OnCollected?.Invoke(this);
    }
}
