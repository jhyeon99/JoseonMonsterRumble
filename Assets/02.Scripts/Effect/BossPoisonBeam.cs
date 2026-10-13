using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>방출된 독액의 속도를 보존하고 중력으로 이동시키는 연결형 물줄기.</summary>
[DisallowMultipleComponent]
public sealed class BossPoisonBeam : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Transform _mouth;
    [SerializeField] private LineRenderer _line;
    [SerializeField] private ParticleSystem _mouthParticles;
    [SerializeField] private ParticleSystem _headParticles;
    [SerializeField] private ParticleSystem _impactCloudParticles;
    [SerializeField] private ParticleSystem _residualParticles;
    [SerializeField] private SpriteRenderer _bossRenderer;
    [SerializeField] private GameObject _attacker;
    [Tooltip("입 기준점에서 입술 바깥까지의 로컬 보정. 보스 이동, 크기, 반전을 따른다.")]
    [SerializeField] private Vector2 _lipOffset = new Vector2(0.12f, 0f);

    [Header("분사 물리")]
    [SerializeField, Min(0f)] private float _telegraphDuration = 0.3f;
    [SerializeField, Min(0.01f)] private float _projectileSpeed = 28f;
    [Tooltip("방출 지속 시간의 거리 기준. 독액 수명과는 별개다.")]
    [SerializeField, Min(0.01f)] private float _maxRange = 18f;
    [SerializeField, Min(0.05f)] private float _liquidLifetime = 0.7f;
    [SerializeField, Min(0f)] private float _airDrag = 0.25f;
    [SerializeField, Range(0f, 1f)] private float _mouthVelocityInheritance = 0.5f;
    [Header("분사 회전 연출")]
    [SerializeField] private bool _enableFollowThrough;
    [Tooltip("보스의 로컬 +X 기준 목표 분사각. 실제 보스 애니메이션을 회전시키지는 않는다.")]
    [SerializeField] private float _followThroughTargetAngle = 120f;
    [SerializeField, Min(0f)] private float _angularSpring = 64f;
    [SerializeField, Min(0f)] private float _angularDamping = 12f;
    [SerializeField, Min(1f)] private float _maxAngularSpeed = 240f;
    [SerializeField, Min(0f)] private float _gravity = 9f;
    [SerializeField, Range(60f, 240f)] private float _samplesPerSecond = 180f;
    [Tooltip("입 기준점의 로컬 +X와 실제 입구 방향 차이를 보정한다.")]
    [SerializeField] private float _mouthAngleOffset;
    [Tooltip("첫 독액이 사거리에 도달할 시간 이후 추가로 방출하는 시간")]
    [SerializeField, Min(0f)] private float _holdDuration = 1.2f;
    [SerializeField, Min(0.01f)] private float _fadeDuration = 0.18f;

    [Header("물줄기")]
    [SerializeField, Min(0.01f)] private float _beamWidth = 1.2f;
    [SerializeField, Range(0.1f, 1f)] private float _mouthWidthRatio = 0.4f;
    [SerializeField, Min(0.01f)] private float _pressureRiseTime = 0.07f;
    [SerializeField, Range(0f, 0.3f)] private float _widthVariation = 0.14f;
    [SerializeField, Min(0.01f)] private float _textureWorldLength = 2f;
    [SerializeField, Min(0f)] private float _textureFlowSpeed = 10f;
    [SerializeField] private string _textureProperty = "_MainTex";
    [Header("끝부분 / 충돌 파편")]
    [SerializeField, Min(0.01f)] private float _endFrayLength = 0.55f;
    [SerializeField, Min(0.01f)] private float _splashInterval = 0.035f;
    [SerializeField, Min(0f)] private float _impactSplashSpeed = 6f;
    [Header("수압 / 입자 분해")]
    [SerializeField, Min(0.05f)] private float _pressureFalloffDuration = 0.55f;
    [SerializeField, Range(0.1f, 0.8f)] private float _breakupPressure = 0.45f;
    [SerializeField, Min(0f)] private float _residualDrag = 1.8f;
    [SerializeField, Min(0.1f)] private float _residualParticleLifetime = 2.4f;
    [Tooltip("종료 독가루의 최소 / 최대 크기. 작은 입자가 더 많이 섞인다.")]
    [SerializeField] private Vector2 _residualDropletSizeRange = new Vector2(0.02f, 0.28f);
    [Tooltip("분해 구간마다 생성하는 독가루의 기준 개수. 실제 개수는 조금씩 다르다.")]
    [SerializeField, Range(1, 12)] private int _residualDropletsPerFragment = 8;
    [SerializeField, ReadOnly] private float _emissionPressure = 1f;

    [Header("충돌 / 피해")]
    [SerializeField] private LayerMask _terrainMask = Constants.Layers.GroundMask;
    [SerializeField] private LayerMask _targetMask = Constants.Layers.PlayerMask;
    [SerializeField, Min(0.001f)] private float _collisionSkin = 0.02f;
    [SerializeField, Min(0f)] private float _damage = 20f;
    [Tooltip("맞으면 걸리는 독 지속 시간 (초). 0이면 독 없음")]
    [SerializeField, Min(0f)] private float _poisonDuration = 10f;
    [SerializeField, Min(0f)] private float _knockbackForce = 2f;
    [SerializeField, Min(0.02f)] private float _hitInterval = 0.35f;
    [SerializeField] private AttackDefense _defense = new AttackDefense { Parryable = false, GuardGaugeCost = 30f };
    [SerializeField] private string _attackName = "독 분사";

    [Header("상태")]
    [SerializeField, ReadOnly] private bool _isFiring;
    [SerializeField, ReadOnly] private bool _hasHitTerrain;
    [SerializeField, ReadOnly] private float _currentLength;

    private struct LiquidSample
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float BirthTime;
        public bool IsBlocked;
        public Vector2 ImpactPoint;
        public Vector2 ImpactNormal;
        public float Pressure;
    }

    private const int SampleCapacity = 512;
    private const int ResidualParticleCapacity = 2048;
    private static readonly WaitForFixedUpdate FixedStep = new WaitForFixedUpdate();
    private readonly LiquidSample[] _samples = new LiquidSample[SampleCapacity];
    private readonly Vector3[] _positions = new Vector3[SampleCapacity + 1];
    private readonly float[] _widths = new float[SampleCapacity + 1];
    private readonly ParticleSystem.Particle[] _residualParticleBuffer = new ParticleSystem.Particle[ResidualParticleCapacity];
    private readonly AnimationCurve _renderWidthCurve = new AnimationCurve();
    private readonly List<RaycastHit2D> _terrainHits = new List<RaycastHit2D>(16);
    private readonly List<Collider2D> _targetHits = new List<Collider2D>(16);
    private readonly HashSet<Collider2D> _checkedTargets = new HashSet<Collider2D>();
    private readonly Dictionary<IDamageable, float> _nextHitTimes = new Dictionary<IDamageable, float>();
    private Coroutine _fireRoutine;
    private Material _runtimeMaterial;
    private Material _originalMaterial;
    private ContactFilter2D _terrainFilter;
    private ContactFilter2D _targetFilter;
    private Vector2 _previousMouthPosition;
    private float _previousMouthAngle;
    private float _simulationTime;
    private float _nextSampleTime;
    private float _nextDropletTime;
    private float _nextSplashTime;
    private Vector2 _impactPoint;
    private Vector2 _impactNormal;
    private Vector2 _lastSplashPoint;
    private bool _hasImpactBurst;
    private float _textureOffset;
    private int _oldestSample;
    private int _sampleCount;
    private int _renderCount;
    private bool _isInitialized;
    private bool _shouldStopEmission;
    private bool _isEmitting;
    private bool _isFollowingThrough;
    private float _followThroughAngle;
    private float _angularVelocity;
    private Vector2 _mouthVelocity;
    private bool _isDepressurizing;
    private ParticleSystemRenderer _impactCloudRenderer;

    public bool IsFiring => _isFiring;
    public bool IsReady => _isInitialized && isActiveAndEnabled;

    /// <summary>프리팹 활성화 전에 실제 입과 보스 참조를 연결한다.</summary>
    public bool SetEmitter(Transform mouth, SpriteRenderer bossRenderer, GameObject attacker)
    {
        if (_isInitialized || mouth == null || bossRenderer == null || attacker == null) return false;
        _mouth = mouth;
        _bossRenderer = bossRenderer;
        _attacker = attacker;
        return true;
    }

    /// <summary>테스트 드라이버가 Awake에서 파티클을 구성한 뒤 호출한다.</summary>
    public void SetParticles(ParticleSystem mouthParticles, ParticleSystem headParticles,
        ParticleSystem impactCloudParticles = null, ParticleSystem residualParticles = null)
    {
        _mouthParticles = mouthParticles;
        _headParticles = headParticles;
        _impactCloudParticles = impactCloudParticles;
        _residualParticles = residualParticles;
    }

    private void Awake()
    {
        if (_mouth == null || _line == null || _mouthParticles == null || _headParticles == null
            || _line.sharedMaterial == null || string.IsNullOrEmpty(_textureProperty)
            || !_line.sharedMaterial.HasProperty(_textureProperty))
        {
            Debug.LogWarning("독 물줄기: 입, LineRenderer, 파티클 또는 Material 참조가 없습니다.", this);
            enabled = false;
            return;
        }
        _originalMaterial = _line.sharedMaterial;
        _runtimeMaterial = new Material(_originalMaterial);
        _line.sharedMaterial = _runtimeMaterial;
        _line.useWorldSpace = true;
        _line.alignment = LineAlignment.View;
        _line.textureMode = LineTextureMode.Stretch;
        _line.numCapVertices = 0;
        _line.numCornerVertices = 0;
        _line.widthMultiplier = _beamWidth;
        _terrainFilter = new ContactFilter2D { useTriggers = false };
        _terrainFilter.SetLayerMask(_terrainMask);
        _targetFilter = new ContactFilter2D { useTriggers = true };
        _targetFilter.SetLayerMask(_targetMask);
        InitializeParticles(_mouthParticles);
        InitializeParticles(_headParticles);
        if (_residualParticles != null)
        {
            InitializeParticles(_residualParticles);
            ParticleSystem.MainModule residualMain = _residualParticles.main;
            residualMain.maxParticles = ResidualParticleCapacity;
            residualMain.gravityModifier = Physics.gravity.y < -0.001f ? _gravity / -Physics.gravity.y : 0f;
        }
        // 끝부분 파편은 자유 분사와 지형 충돌의 실제 방향으로 직접 방출한다.
        ParticleSystem.EmissionModule headEmission = _headParticles.emission;
        headEmission.enabled = false;
        if (_impactCloudParticles != null)
        {
            InitializeParticles(_impactCloudParticles);
            _impactCloudRenderer = _impactCloudParticles.GetComponent<ParticleSystemRenderer>();
            ParticleSystem.EmissionModule cloudEmission = _impactCloudParticles.emission;
            cloudEmission.enabled = false;
        }
        if (_bossRenderer != null)
        {
            _line.sortingLayerID = _bossRenderer.sortingLayerID;
            _line.sortingOrder = _bossRenderer.sortingOrder + 1;
            SetParticleSorting(_mouthParticles);
            SetParticleSorting(_headParticles);
            if (_residualParticles != null) SetParticleSorting(_residualParticles);
            if (_impactCloudParticles != null)
            {
                SetParticleSorting(_impactCloudParticles);
                _impactCloudRenderer.sortingOrder = _line.sortingOrder + 1;
            }
        }
        _isInitialized = true;
        ResetBeam();
    }

    private static void InitializeParticles(ParticleSystem particles)
    {
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
    }

    private void SetParticleSorting(ParticleSystem particles)
    {
        foreach (ParticleSystemRenderer renderer in particles.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            renderer.sortingLayerID = _line.sortingLayerID;
            renderer.sortingOrder = _line.sortingOrder;
        }
    }

    private Vector3 GetLipPosition()
    {
        Vector3 position = _mouth.TransformPoint(new Vector3(_lipOffset.x, _lipOffset.y, 0f));
        if (_bossRenderer == null) return position;
        Vector3 local = _bossRenderer.transform.InverseTransformPoint(position);
        if (_bossRenderer.flipX) local.x = -local.x;
        if (_bossRenderer.flipY) local.y = -local.y;
        return _bossRenderer.transform.TransformPoint(local);
    }

    private float GetEmissionAngle()
    {
        float offset = _mouthAngleOffset * Mathf.Deg2Rad;
        Vector3 direction = _mouth.TransformVector(new Vector3(Mathf.Cos(offset), Mathf.Sin(offset), 0f));
        if (_bossRenderer != null)
        {
            Vector3 local = _bossRenderer.transform.InverseTransformVector(direction);
            if (_bossRenderer.flipX) local.x = -local.x;
            if (_bossRenderer.flipY) local.y = -local.y;
            direction = _bossRenderer.transform.TransformVector(local);
        }
        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    }

    private void UpdateMouthParticles()
    {
        _mouthParticles.transform.SetPositionAndRotation(GetLipPosition(), Quaternion.Euler(0f, 0f, GetEmissionAngle()));
    }

    [ContextMenu("독 물줄기 테스트 발사")]
    public void StartFire()
    {
        if (!_isInitialized || !isActiveAndEnabled) return;
        StopFire();
        _fireRoutine = StartCoroutine(FireBeam());
    }

    [ContextMenu("독 물줄기 즉시 중지")]
    public void StopFire()
    {
        if (_fireRoutine != null) StopCoroutine(_fireRoutine);
        _fireRoutine = null;
        if (_isInitialized) ResetBeam();
    }

    public void StopEmission() => _shouldStopEmission = true;

    public void StartFollowThrough()
    {
        if (!_enableFollowThrough || !_isEmitting || _isFollowingThrough) return;
        _isFollowingThrough = true;
        _followThroughAngle = _previousMouthAngle;
    }

    private float CalculateEmissionAngle(float dt)
    {
        float authoredAngle = GetEmissionAngle();
        if (!_isFollowingThrough)
        {
            float measuredVelocity = Mathf.DeltaAngle(_previousMouthAngle, authoredAngle) / dt;
            _angularVelocity = Mathf.Lerp(_angularVelocity, measuredVelocity, 1f - Mathf.Exp(-dt / 0.08f));
            return authoredAngle;
        }
        float radians = _followThroughTargetAngle * Mathf.Deg2Rad;
        var target = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f);
        if (_bossRenderer != null)
        {
            if (_bossRenderer.flipX) target.x = -target.x;
            if (_bossRenderer.flipY) target.y = -target.y;
            target = _bossRenderer.transform.TransformVector(target);
        }
        float targetAngle = Mathf.Atan2(target.y, target.x) * Mathf.Rad2Deg;
        float acceleration = Mathf.DeltaAngle(_followThroughAngle, targetAngle) * _angularSpring - _angularVelocity * _angularDamping;
        _angularVelocity = Mathf.Clamp(_angularVelocity + acceleration * dt, -_maxAngularSpeed, _maxAngularSpeed);
        _followThroughAngle += _angularVelocity * dt;
        return _followThroughAngle;
    }

    private Vector2 CalculateLiquidDisplacement(Vector2 velocity, Vector2 acceleration, float dt)
    {
        float drag = _isDepressurizing ? _residualDrag : _airDrag;
        if (drag < 0.001f) return velocity * dt + acceleration * (0.5f * dt * dt);
        float velocityIntegral = (1f - Mathf.Exp(-drag * dt)) / drag;
        return velocity * velocityIntegral + acceleration * ((dt - velocityIntegral) / drag);
    }

    private Vector2 CalculateLiquidVelocity(Vector2 velocity, Vector2 acceleration, float dt)
    {
        float drag = _isDepressurizing ? _residualDrag : _airDrag;
        if (drag < 0.001f) return velocity + acceleration * dt;
        float attenuation = Mathf.Exp(-drag * dt);
        return velocity * attenuation + acceleration * ((1f - attenuation) / drag);
    }

    private IEnumerator FireBeam()
    {
        _isFiring = true;
        UpdateMouthParticles();
        _mouthParticles.Play(true);
        float elapsed = 0f;
        while (elapsed < _telegraphDuration && !_shouldStopEmission)
        {
            yield return FixedStep;
            UpdateMouthParticles();
            elapsed += Time.fixedDeltaTime;
        }
        _previousMouthPosition = GetLipPosition();
        _previousMouthAngle = GetEmissionAngle();
        _headParticles.transform.SetPositionAndRotation(GetLipPosition(), Quaternion.Euler(0f, 0f, _previousMouthAngle));
        _headParticles.Play(true);
        _isEmitting = true;
        float duration = _maxRange / _projectileSpeed + _holdDuration;
        elapsed = 0f;
        while (elapsed < duration && !_shouldStopEmission)
        {
            yield return FixedStep;
            UpdateLiquid(Time.fixedDeltaTime);
            ApplyBeamDamage();
            elapsed += Time.fixedDeltaTime;
        }
        _isDepressurizing = true;
        _mouthParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        _headParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (_impactCloudParticles != null) _impactCloudParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        ParticleSystem.LimitVelocityOverLifetimeModule resistance = _headParticles.limitVelocityOverLifetime;
        resistance.enabled = true;
        resistance.limit = 1000f;
        resistance.dampen = 0f;
        resistance.drag = _residualDrag;
        // 입에서의 연결과 약한 방출을 유지하며 압력을 낮춘다.
        // 굵은 줄기를 입에서 떼어 날려 보내는 구간은 만들지 않는다.
        elapsed = 0f;
        while (_sampleCount > 0 && _emissionPressure > _breakupPressure && elapsed < _pressureFalloffDuration)
        {
            yield return FixedStep;
            elapsed += Time.fixedDeltaTime;
            _emissionPressure = 1f - Mathf.SmoothStep(0f, 1f, elapsed / _pressureFalloffDuration);
            UpdateLiquid(Time.fixedDeltaTime);
        }
        if (_residualParticles != null)
        {
            ParticleSystem.LimitVelocityOverLifetimeModule residualResistance = _residualParticles.limitVelocityOverLifetime;
            residualResistance.enabled = true;
            residualResistance.limit = 1000f;
            residualResistance.dampen = 0f;
            residualResistance.drag = _residualDrag;
            _residualParticles.Play(true);
        }
        // 압력이 분해 기준에 도달하면 전체 줄기를 같은 물리 프레임에 방울로 바꾼다.
        // 특정 끝점부터 길이를 줄이며 남은 굵은 줄기를 날려 보내지 않는다.
        _isEmitting = false;
        BreakUpStream();
        if (_residualParticles != null) _residualParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        _headParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (_impactCloudParticles != null) _impactCloudParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        _line.enabled = false;
        while (_mouthParticles.IsAlive(true) || _headParticles.IsAlive(true)
            || (_impactCloudParticles != null && _impactCloudParticles.IsAlive(true))
            || (_residualParticles != null && _residualParticles.IsAlive(true)))
        {
            yield return FixedStep;
            UpdateResidualDropletDrag(Time.fixedDeltaTime);
        }

        ResetBeam();
        _fireRoutine = null;
    }

    private float GetSampleLifetime() => _liquidLifetime + (_isDepressurizing ? _pressureFalloffDuration : 0f);

    private void UpdateLiquid(float dt)
    {
        float stepStart = _simulationTime;
        _simulationTime += dt;
        Vector2 acceleration = Vector2.down * _gravity;
        float drag = _isDepressurizing ? _residualDrag : _airDrag;
        float attenuation = drag < 0.001f ? 1f : Mathf.Exp(-drag * dt);
        float velocityIntegral = drag < 0.001f ? dt : (1f - attenuation) / drag;
        Vector2 gravityDisplacement = acceleration * (drag < 0.001f ? 0.5f * dt * dt : (dt - velocityIntegral) / drag);
        Vector2 gravityVelocity = acceleration * velocityIntegral;
        for (int i = 0; i < _sampleCount; i++)
        {
            int index = (_oldestSample + i) % SampleCapacity;
            LiquidSample sample = _samples[index];
            if (sample.IsBlocked) continue;
            // 같은 시간 간격의 감쇠·중력 계수는 모든 기존 독액이 공유한다.
            Vector2 displacement = sample.Velocity * velocityIntegral + gravityDisplacement;
            RaycastHit2D hit = GetTerrainHit(sample.Position, CalculateWidth(sample) * _beamWidth * 0.5f,
                displacement.normalized, displacement.magnitude);
            sample.Position += hit.collider != null ? displacement.normalized * Mathf.Max(0f, hit.distance - _collisionSkin) : displacement;
            sample.IsBlocked = hit.collider != null;
            if (sample.IsBlocked)
            {
                sample.ImpactPoint = hit.point;
                sample.ImpactNormal = hit.normal;
            }
            sample.Velocity = sample.Velocity * attenuation + gravityVelocity;
            _samples[index] = sample;
        }
        while (_sampleCount > 0 && _simulationTime - _samples[_oldestSample].BirthTime >= GetSampleLifetime())
        {
            _oldestSample = (_oldestSample + 1) % SampleCapacity;
            _sampleCount--;
        }
        if (_isEmitting)
        {
            if (!_isDepressurizing) _emissionPressure = 1f;
            Vector2 position = GetLipPosition();
            float angle = CalculateEmissionAngle(dt);
            var measuredMouthVelocity = Vector2.ClampMagnitude((position - _previousMouthPosition) / dt, 12f);
            _mouthVelocity = Vector2.Lerp(_mouthVelocity, measuredMouthVelocity, 1f - Mathf.Exp(-dt / 0.08f));
            while (_nextSampleTime <= _simulationTime)
            {
                float t = Mathf.Clamp01((_nextSampleTime - stepStart) / dt);
                var spawnPosition = Vector2.Lerp(_previousMouthPosition, position, t);
                float spawnAngle = Mathf.LerpAngle(_previousMouthAngle, angle, t) * Mathf.Deg2Rad;
                Vector2 velocity = new Vector2(Mathf.Cos(spawnAngle), Mathf.Sin(spawnAngle))
                    * (_projectileSpeed * Mathf.Lerp(0.28f, 1.25f, _emissionPressure))
                    + _mouthVelocity * _mouthVelocityInheritance;
                float age = _simulationTime - _nextSampleTime;
                Vector2 displacement = CalculateLiquidDisplacement(velocity, acceleration, age);
                RaycastHit2D hit = GetTerrainHit(spawnPosition, _beamWidth * _mouthWidthRatio * 0.5f,
                    displacement.normalized, displacement.magnitude);
                var sample = new LiquidSample
                {
                    Position = spawnPosition + (hit.collider != null ? displacement.normalized * Mathf.Max(0f, hit.distance - _collisionSkin) : displacement),
                    Velocity = CalculateLiquidVelocity(velocity, acceleration, age),
                    BirthTime = _nextSampleTime,
                    IsBlocked = hit.collider != null,
                    ImpactPoint = hit.point,
                    ImpactNormal = hit.normal,
                    Pressure = _emissionPressure
                };
                AddSample(sample);

                _nextSampleTime += 1f / _samplesPerSecond;
            }
            _previousMouthPosition = position;
            _previousMouthAngle = angle;
            _mouthParticles.transform.SetPositionAndRotation(GetLipPosition(), Quaternion.Euler(0f, 0f, angle));
            ParticleSystem.EmissionModule mouthEmission = _mouthParticles.emission;
            mouthEmission.rateOverTime = Mathf.Lerp(8f, 180f, _emissionPressure);
            ParticleSystem.MainModule mouthMain = _mouthParticles.main;
            mouthMain.startSpeed = new ParticleSystem.MinMaxCurve(12f * _emissionPressure, 26f * _emissionPressure);
        }
        UpdateBeamGeometry();
    }

    private void BreakUpStream()
    {
        // 전체 위치에서 동시에 변환한다. 간격은 샘플 개수가 아니라 실제 거리로 정한다.
        // 이 루프 안에서는 시간을 진행하거나 남은 줄기를 다시 그리지 않는다.
        _line.enabled = false;
        _line.positionCount = 0;
        while (_sampleCount > 0)
        {
            LiquidSample sample = _samples[(_oldestSample + _sampleCount - 1) % SampleCapacity];
            Vector2 previous = sample.Position;
            float distance = 0f;
            float spacing = Random.Range(0.12f, 0.38f);
            int groupCount = 0;
            while (_sampleCount > 0 && distance < spacing)
            {
                LiquidSample next = _samples[(_oldestSample + _sampleCount - 1) % SampleCapacity];
                // 벽에 멈춘 구간과 자유롭게 이동하는 구간을 한 덩어리로 묶지 않는다.
                if (groupCount > 0 && next.IsBlocked != sample.IsBlocked) break;
                distance += Vector2.Distance(previous, next.Position);
                previous = next.Position;
                _sampleCount--;
                groupCount++;
            }
            sample.Position = Vector2.Lerp(sample.Position, previous, Random.Range(0.25f, 0.75f));
            if (!sample.IsBlocked) EmitLiquidFragment(sample, _beamWidth * CalculateWidth(sample));
        }
        _oldestSample = _renderCount = 0;
        _currentLength = 0f;
    }

    private void EmitLiquidFragment(LiquidSample sample, float width)
    {
        ParticleSystem particles = _residualParticles != null ? _residualParticles : _headParticles;
        Vector2 tangent = sample.Velocity.sqrMagnitude > 0.001f ? sample.Velocity.normalized : Vector2.right;
        var normal = new Vector2(-tangent.y, tangent.x);
        int droplets = Random.Range(Mathf.Max(1, _residualDropletsPerFragment - 2), _residualDropletsPerFragment + 3);
        bool hasChunk = Random.value < 0.18f;
        for (int i = 0; i < droplets; i++)
        {
            bool isChunk = hasChunk && i == 0;
            float sizeVariation = Random.value;
            float size = isChunk
                ? Mathf.Clamp(width * Random.Range(0.16f, 0.3f), 0.12f, 0.4f)
                : Mathf.Lerp(_residualDropletSizeRange.x, _residualDropletSizeRange.y, sizeVariation * sizeVariation);
            Vector2 offset = normal * Random.Range(-width * 0.25f, width * 0.25f) + tangent * Random.Range(-0.1f, 0.1f);
            var fragment = new ParticleSystem.EmitParams
            {
                position = new Vector3(sample.Position.x + offset.x, sample.Position.y + offset.y, _mouth.position.z),
                velocity = sample.Velocity + normal * Random.Range(-1.2f, 1.2f),
                startSize = size,
                startLifetime = _residualParticleLifetime * (isChunk ? Random.Range(0.15f, 0.3f) : Random.Range(0.35f, 0.7f))
            };
            particles.Emit(fragment, 1);
        }
    }

    private void UpdateResidualDropletDrag(float dt)
    {
        if (_residualParticles == null || _residualDrag <= 0f) return;
        int count = _residualParticles.GetParticles(_residualParticleBuffer);
        for (int i = 0; i < count; i++)
        {
            ParticleSystem.Particle particle = _residualParticleBuffer[i];
            float extraDrag = _residualDrag * Mathf.Clamp(0.12f / Mathf.Max(0.025f, particle.startSize) - 1f, 0f, 3f);
            particle.velocity *= Mathf.Exp(-extraDrag * dt);
            _residualParticleBuffer[i] = particle;
        }
        if (count > 0) _residualParticles.SetParticles(_residualParticleBuffer, count);
    }

    private void AddSample(LiquidSample sample)
    {
        if (_sampleCount == SampleCapacity)
        {
            _oldestSample = (_oldestSample + 1) % SampleCapacity;
            _sampleCount--;
        }
        _samples[(_oldestSample + _sampleCount) % SampleCapacity] = sample;
        _sampleCount++;
    }

    private float CalculateWidth(LiquidSample sample)
    {
        float age = _simulationTime - sample.BirthTime;
        float width = Mathf.Lerp(_mouthWidthRatio, 1f, Mathf.SmoothStep(0f, 1f, age / _pressureRiseTime));
        width *= Mathf.Lerp(0.4f, 1.4f, sample.Pressure);
        // 태어난 시각의 굵기 변화는 독액과 함께 이동한다. 중심선을 흔들지 않는다.
        width *= 1f + _widthVariation * Mathf.Sin(sample.BirthTime * 31f) * Mathf.Sin(sample.BirthTime * 17f + 1f);
        if (_isDepressurizing)
            width *= Mathf.Sqrt(Mathf.Clamp01(_emissionPressure));
        return Mathf.Max(0.001f, width);
    }

    private RaycastHit2D GetTerrainHit(Vector2 origin, float radius, Vector2 direction, float distance)
    {
        _terrainHits.Clear();
        Physics2D.CircleCast(origin, radius, direction, _terrainFilter, _terrainHits, distance);
        RaycastHit2D nearest = default;
        for (int i = 0; i < _terrainHits.Count; i++)
        {
            if (nearest.collider == null || _terrainHits[i].distance < nearest.distance)
                nearest = _terrainHits[i];
        }
        return nearest;
    }

    private void UpdateBeamGeometry()
    {
        _renderCount = 0;
        _currentLength = 0f;
        _hasHitTerrain = false;
        _impactNormal = Vector2.zero;
        if (_isEmitting)
        {
            _positions[_renderCount] = GetLipPosition();
            _widths[_renderCount++] = _mouthWidthRatio * (_isDepressurizing ? Mathf.Sqrt(Mathf.Clamp01(_emissionPressure)) : 1f);
        }
        for (int i = _sampleCount - 1; i >= 0; i--)
        {
            LiquidSample sample = _samples[(_oldestSample + i) % SampleCapacity];
            Vector2 position = sample.Position;
            float width = CalculateWidth(sample);
            if (_renderCount > 0)
            {
                Vector2 previous = _positions[_renderCount - 1];
                Vector2 displacement = position - previous;
                if (displacement.magnitude < 0.001f) continue;
                RaycastHit2D hit = GetTerrainHit(previous, _beamWidth * Mathf.Min(width, _widths[_renderCount - 1]) * 0.5f,
                    displacement.normalized, displacement.magnitude);
                if (hit.collider != null)
                {
                    position = previous + displacement.normalized * Mathf.Max(0f, hit.distance - _collisionSkin);
                    _hasHitTerrain = true;
                    _impactPoint = hit.point;
                    _impactNormal = hit.normal;
                }
                _currentLength += Vector2.Distance(previous, position);
            }
            if (sample.IsBlocked && !_hasHitTerrain)
            {
                _impactPoint = sample.ImpactPoint;
                _impactNormal = sample.ImpactNormal;
            }
            if (sample.IsBlocked || _hasHitTerrain)
            {
                // 판정용 독액은 벽 앞에서 정지시키고, 표시 끝점은 실제 접촉면까지 잇는다.
                Vector2 contact = _impactPoint + _impactNormal * _collisionSkin;
                if (_renderCount > 0)
                {
                    _currentLength += Vector2.Distance(_positions[_renderCount - 1], contact)
                        - Vector2.Distance(_positions[_renderCount - 1], position);
                }

                position = contact;
            }
            _positions[_renderCount] = new Vector3(position.x, position.y, _mouth.position.z);
            _widths[_renderCount++] = width;
            if (sample.IsBlocked || _hasHitTerrain)
            {
                _hasHitTerrain = true;
                break;
            }
        }
        _line.enabled = _renderCount > 1 && _currentLength > 0.001f;
        _line.positionCount = _renderCount;
        _renderWidthCurve.ClearKeys();
        float arcLength = 0f;
        for (int i = 0; i < _renderCount; i++)
        {
            _line.SetPosition(i, _positions[i]);
            if (i > 0) arcLength += Vector3.Distance(_positions[i - 1], _positions[i]);
            _renderWidthCurve.AddKey(new Keyframe(arcLength / Mathf.Max(0.001f, _currentLength), _widths[i]));
        }
        _line.widthCurve = _renderWidthCurve;
        if (_renderCount > 1)
        {
            Vector2 direction = (_positions[_renderCount - 1] - _positions[_renderCount - 2]).normalized;
            _headParticles.transform.SetPositionAndRotation(_positions[_renderCount - 1],
                Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg));
            if (!_isDepressurizing)
            {
                EmitEdgeDroplet();
                EmitStreamEndSplash(direction);
            }
        }
        _textureOffset = Mathf.Repeat(_textureOffset - _textureFlowSpeed * Time.fixedDeltaTime / _textureWorldLength, 1f);
        _runtimeMaterial.SetTextureScale(_textureProperty, new Vector2(_currentLength / _textureWorldLength, 1f));
        _runtimeMaterial.SetTextureOffset(_textureProperty, new Vector2(_textureOffset, 0f));
        _runtimeMaterial.SetFloat("_StreamLength", _currentLength);
        _runtimeMaterial.SetFloat("_EndFrayLength", _endFrayLength);
        _runtimeMaterial.SetFloat("_EndImpact", _hasHitTerrain ? 1f : 0f);
        _runtimeMaterial.SetFloat("_HasMouthConnection", _isEmitting ? 1f : 0f);
    }

    private void EmitStreamEndSplash(Vector2 direction)
    {
        if (!_hasHitTerrain) _hasImpactBurst = false;
        bool shouldBurst = _hasHitTerrain && (!_hasImpactBurst || Vector2.Distance(_lastSplashPoint, _impactPoint) > 0.75f);
        if ((!shouldBurst && _simulationTime < _nextSplashTime) || !_line.enabled) return;
        _nextSplashTime = _simulationTime + _splashInterval;
        Vector2 normal = _hasHitTerrain && _impactNormal.sqrMagnitude > 0f ? _impactNormal.normalized : direction;
        var side = new Vector2(-normal.y, normal.x);
        Vector3 origin = _hasHitTerrain
            ? new Vector3(_impactPoint.x, _impactPoint.y, _mouth.position.z) + (Vector3)(normal * _collisionSkin)
            : _positions[_renderCount - 1];
        int count = _hasHitTerrain ? (shouldBurst ? 24 : 10) : 3;
        if (_hasHitTerrain)
        {
            _hasImpactBurst = true;
            _lastSplashPoint = _impactPoint;
            EmitImpactCloud(origin, normal, side, shouldBurst);
        }
        for (int i = 0; i < count; i++)
        {
            Vector2 velocity = _hasHitTerrain
                ? normal * Random.Range(2f, shouldBurst ? 7f : 4f) + side * Random.Range(-_impactSplashSpeed, _impactSplashSpeed)
                : direction * (_projectileSpeed * Random.Range(0.4f, 0.75f)) + side * Random.Range(-2f, 2f);
            var emission = new ParticleSystem.EmitParams
            {
                position = origin + (Vector3)(side * Random.Range(-0.3f, 0.3f)),
                velocity = velocity,
                startLifetime = Random.Range(0.12f, shouldBurst ? 0.38f : 0.26f),
                startSize = Random.Range(_hasHitTerrain ? 0.18f : 0.08f, shouldBurst ? 0.85f : (_hasHitTerrain ? 0.6f : 0.28f))
            };
            _headParticles.Emit(emission, 1);
        }
    }

    private void EmitImpactCloud(Vector3 origin, Vector2 normal, Vector2 side, bool isBurst)
    {
        if (_impactCloudParticles == null) return;
        _impactCloudParticles.Play(true);
        int count = isBurst ? 18 : 3;
        for (int i = 0; i < count; i++)
        {
            var cloud = new ParticleSystem.EmitParams
            {
                position = origin + (Vector3)(normal * Random.Range(0.15f, 0.7f) + side * Random.Range(-0.65f, 0.65f)),
                velocity = normal * Random.Range(1.5f, 4f) + side * Random.Range(-4f, 4f),
                startSize = Random.Range(isBurst ? 1.3f : 0.9f, isBurst ? 2.5f : 1.8f),
                startLifetime = Random.Range(0.25f, 0.5f)
            };
            _impactCloudParticles.Emit(cloud, 1);
        }
    }

    private void EmitEdgeDroplet()
    {
        if (_simulationTime < _nextDropletTime || _renderCount < 3) return;
        _nextDropletTime = _simulationTime + 0.035f;
        int index = Random.Range(1, _renderCount - 1);
        Vector2 tangent = (_positions[index + 1] - _positions[index - 1]).normalized;
        Vector2 normal = new Vector2(-tangent.y, tangent.x) * (Random.value < 0.5f ? -1f : 1f);
        var emission = new ParticleSystem.EmitParams
        {
            position = _positions[index] + (Vector3)(normal * _widths[index] * _beamWidth * 0.45f),
            velocity = tangent * (_projectileSpeed * 0.65f) + normal * 1.5f,
            startLifetime = 0.25f,
            startSize = Random.Range(0.06f, 0.14f)
        };
        _headParticles.Emit(emission, 1);
    }

    private void ApplyBeamDamage()
    {
        if (!_line.enabled || _damage <= 0f || _renderCount < 2) return;
        // 세부 판정은 유지한다. 전체 줄기 주변에 대상이 없을 때만 건너뛴다.
        Vector2 minimum = _positions[0];
        Vector2 maximum = minimum;
        float maximumWidth = _widths[0];
        for (int i = 1; i < _renderCount; i++)
        {
            minimum = Vector2.Min(minimum, _positions[i]);
            maximum = Vector2.Max(maximum, _positions[i]);
            maximumWidth = Mathf.Max(maximumWidth, _widths[i]);
        }
        _targetHits.Clear();
        Physics2D.OverlapBox((minimum + maximum) * 0.5f,
            maximum - minimum + Vector2.one * (maximumWidth * _beamWidth + _collisionSkin * 2f),
            0f, _targetFilter, _targetHits);
        if (_targetHits.Count == 0) return;
        _checkedTargets.Clear();
        for (int segment = 1; segment < _renderCount; segment++)
        {
            Vector2 previous = _positions[segment - 1];
            Vector2 current = _positions[segment];
            Vector2 displacement = current - previous;
            float angle = Mathf.Atan2(displacement.y, displacement.x) * Mathf.Rad2Deg;
            float width = _beamWidth * Mathf.Min(_widths[segment - 1], _widths[segment]);
            Vector2 center = (previous + current) * 0.5f;
            _targetHits.Clear();
            Physics2D.OverlapBox(center, new Vector2(displacement.magnitude, width), angle, _targetFilter, _targetHits);
            for (int i = 0; i < _targetHits.Count; i++)
            {
                Collider2D collider = _targetHits[i];
                if (collider == null || !_checkedTargets.Add(collider)) continue;
                IDamageable receiver = collider.GetComponentInParent<IDamageable>();
                if (receiver == null || (_attacker != null && collider.transform.IsChildOf(_attacker.transform))) continue;
                if (_nextHitTimes.TryGetValue(receiver, out float nextHitTime) && Time.time < nextHitTime) continue;
                _nextHitTimes[receiver] = Time.time + _hitInterval;
                var hit = new HitData(_damage, _knockbackForce, collider.ClosestPoint(center), displacement.normalized,
                    _defense, attackName: _attackName);
                hit.Attacker = _attacker != null ? _attacker : gameObject;
                hit.PoisonDuration = _poisonDuration;
                receiver.TakeDamage(hit);
            }
        }
    }

    private void ResetBeam()
    {
        _isFiring = false;
        _isEmitting = false;
        _isFollowingThrough = false;
        _isDepressurizing = false;
        _emissionPressure = 1f;
        _angularVelocity = 0f;
        _mouthVelocity = Vector2.zero;
        _shouldStopEmission = false;
        _hasHitTerrain = false;
        _currentLength = _simulationTime = _nextSampleTime = _nextDropletTime = _textureOffset = 0f;
        _nextSplashTime = 0f;
        _hasImpactBurst = false;
        _sampleCount = _oldestSample = _renderCount = 0;
        _nextHitTimes.Clear();
        _checkedTargets.Clear();
        _line.enabled = false;
        _line.positionCount = 0;
        _line.startColor = _line.endColor = Color.white;
        _mouthParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _headParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_residualParticles != null) _residualParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.LimitVelocityOverLifetimeModule resistance = _headParticles.limitVelocityOverLifetime;
        resistance.enabled = false;
        if (_impactCloudParticles != null) _impactCloudParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnDisable() => StopFire();

    private void OnValidate()
    {
        _projectileSpeed = Mathf.Max(0.01f, _projectileSpeed);
        _maxRange = Mathf.Max(0.01f, _maxRange);
        _samplesPerSecond = Mathf.Clamp(_samplesPerSecond, 60f, 240f);
        _liquidLifetime = Mathf.Clamp(_liquidLifetime, 0.05f, (SampleCapacity - 2f) / _samplesPerSecond);
        _pressureRiseTime = Mathf.Max(0.01f, _pressureRiseTime);
        _beamWidth = Mathf.Max(0.01f, _beamWidth);
        _textureWorldLength = Mathf.Max(0.01f, _textureWorldLength);
        _hitInterval = Mathf.Max(0.02f, _hitInterval);
        _fadeDuration = Mathf.Max(0.01f, _fadeDuration);
        _residualDropletSizeRange.x = Mathf.Max(0.005f, _residualDropletSizeRange.x);
        _residualDropletSizeRange.y = Mathf.Max(_residualDropletSizeRange.x, _residualDropletSizeRange.y);
        _residualDropletsPerFragment = Mathf.Clamp(_residualDropletsPerFragment, 1, 12);
    }

    private void OnDestroy()
    {
        if (_runtimeMaterial == null) return;
        if (_line != null) _line.sharedMaterial = _originalMaterial;
        Destroy(_runtimeMaterial);
    }
}
