using System.Collections;
using UnityEngine;

public class PlayerProjectile : Projectile
{
    private const float PositionEpsilon = 0.01f;
    private static readonly float[] TrajectoryAngleFactors = { 1f, -0.82f, 0.18f, 0.62f, -0.48f };
    private static readonly float[] HomingDurationFactors = { 1.15f, 0.88f, 0.75f, 1.02f, 0.82f };
    private static readonly float[] TargetHeightFactors = { 0.12f, -0.1f, 0f, 0.06f, -0.05f };

    private static readonly int AnimParamFire = Animator.StringToHash("fire");
    private static readonly int AnimStateExplosion = Animator.StringToHash("Talisman_Explosion");
    private static readonly Collider2D[] Hits = new Collider2D[16];

    [Header("탐색")]
    [SerializeField] private float _searchRadius = 8f;
    [SerializeField] private LayerMask _enemyLayer;
    [SerializeField] private LayerMask _obstacleLayer;
    [SerializeField] private Vector2 _sightOffset = new Vector2(0f, 0.5f);
    [SerializeField] private bool _drawDebug = false;

    [Header("유도")]
    [SerializeField] private float _lifeTime = 3f;
    [SerializeField, Range(0f, 180f)] private float _trajectorySpread = 60f;
    [SerializeField, Min(0.01f)] private float _homingDuration = 0.9f;

    [Header("속도 변화")]
    [SerializeField, Min(0.01f)] private float _accelerationDuration = 0.65f;
    [SerializeField]
    private AnimationCurve _speedCurve = new AnimationCurve(
        new Keyframe(0f, 0.36f),
        new Keyframe(0.35f, 0.45f),
        new Keyframe(0.72f, 1.15f),
        new Keyframe(1f, 2.4f));
    private Rigidbody2D _rigidbody2D;
    private Collider2D _collider2D;
    private Animator _animator;
    private Collider2D _targetCollider;
    private IDamageable _targetDamageable;
    private EnemyHealth _targetHealth;
    private Vector2 _dir;
    private Vector2 _launchDirection;
    private int _facing = 1;
    private int _trajectoryIndex;
    private int _trajectoryCount = 1;
    private float _initialTargetDistance;
    private float _homingStartTime;
    private bool _initialized;
    private bool _isExploding;
    private Coroutine _explodeRoutine;
    private Transform _explosionTarget;
    private Vector3 _explosionLocalPosition;

    public void SetFacing(int facing) => _facing = facing >= 0 ? 1 : -1;

    public void SetTrajectory(int trajectoryIndex, int trajectoryCount)
    {
        _trajectoryIndex = Mathf.Max(0, trajectoryIndex);
        _trajectoryCount = Mathf.Max(1, trajectoryCount);
    }

    protected override Vector2 HitDirection =>
        _dir != Vector2.zero ? _dir : (_facing > 0 ? Vector2.right : Vector2.left);

    public override void OnSpawn(Transform muzzlePoint, Transform playerPoint, float damageIncreaseValue)
    {
        base.OnSpawn(muzzlePoint, playerPoint, damageIncreaseValue);

        transform.position = muzzlePoint.position;
        EnsureComponents();

        _isExploding = false;
        if (_collider2D != null)
        {
            _collider2D.enabled = true;
        }

        if (_animator != null)
        {
            _animator.ResetTrigger(AnimParamFire);
            _animator.SetTrigger(AnimParamFire);
        }
    }

    protected override void Fire()
    {
        if (!_isLaunching || _isExploding)
        {
            return;
        }

        if (!_initialized)
        {
            Initialize();
        }

        if (_targetCollider != null && _targetCollider.isActiveAndEnabled)
        {
            Vector2 targetPosition = CalculateTargetPosition(_targetCollider);
            Vector2 toTarget = (targetPosition - _rigidbody2D.position).normalized;
            float launchProgress = CalculateLaunchProgress(targetPosition);
            float homingProgress = CalculateHomingProgress();
            _dir = Vector3.Slerp(_launchDirection, toTarget, homingProgress).normalized;
            ApplyVelocity(launchProgress);
        }
        else
        {
            StopTrackingTarget();
            ApplyVelocity(CalculateTimeProgress());
        }

        _t += Time.deltaTime;
        if (_t >= _lifeTime)
        {
            _isLaunching = false;
            gameObject.SetActive(false);
        }
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (_isExploding)
        {
            return;
        }

        if (!EnemyHurtbox.TryGetDamageTarget(other, out IDamageable damageable))
        {
            return;
        }

        Vector2 hitPoint = other.ClosestPoint(transform.position);
        var hitData = new HitData(
            ProjectileDamage,
            KnockbackForce,
            hitPoint,
            HitDirection,
            isTalisman: true
        );

        bool damageAccepted = damageable.TakeDamage(hitData);
        if (!damageAccepted)
        {
            if (ReferenceEquals(damageable, _targetDamageable))
            {
                Retarget(damageable);
            }

            return;
        }

        StartExplosion(other);
    }

    private void LateUpdate()
    {
        if (_isExploding && _explosionTarget != null)
        {
            transform.position = _explosionTarget.TransformPoint(_explosionLocalPosition);
        }
    }

    private void StartExplosion(Collider2D hitCollider)
    {
        _isExploding = true;
        _isLaunching = false;

        SnapToHitTarget(hitCollider);
        StopTrackingTarget();

        if (_collider2D != null)
        {
            _collider2D.enabled = false;
        }

        if (_rigidbody2D != null)
        {
            _rigidbody2D.linearVelocity = Vector2.zero;
        }

        if (_animator != null)
        {
            _animator.Play(AnimStateExplosion, 0, 0f);
        }

        if (_explodeRoutine != null)
        {
            StopCoroutine(_explodeRoutine);
        }

        _explodeRoutine = StartCoroutine(WaitForExplosionRoutine());
    }

    private void SnapToHitTarget(Collider2D hitCollider)
    {
        if (_collider2D == null || hitCollider == null)
        {
            return;
        }

        ColliderDistance2D hitDistance = _collider2D.Distance(hitCollider);
        transform.position = hitDistance.pointB;
        _explosionTarget = hitCollider.transform;
        _explosionLocalPosition = _explosionTarget.InverseTransformPoint(transform.position);
    }

    private IEnumerator WaitForExplosionRoutine()
    {
        if (_animator == null)
        {
            Debug.LogWarning("[PlayerProjectile] 폭발 애니메이터가 없어 투사체를 즉시 비활성화합니다.", this);
            _explodeRoutine = null;
            gameObject.SetActive(false);
            yield break;
        }

        AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        while (stateInfo.shortNameHash != AnimStateExplosion)
        {
            yield return null;
            stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        }

        while (stateInfo.shortNameHash == AnimStateExplosion && stateInfo.normalizedTime < 1f)
        {
            yield return null;
            stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        }

        _explodeRoutine = null;
        gameObject.SetActive(false);
    }

    private void EnsureComponents()
    {
        if (_rigidbody2D == null)
        {
            _rigidbody2D = GetComponent<Rigidbody2D>();
        }

        if (_collider2D == null)
        {
            _collider2D = GetComponent<Collider2D>();
        }

        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>();
        }

    }

    private void Initialize()
    {
        EnsureComponents();

        SetTarget(FindNearestEnemy(null));

        if (_targetCollider != null)
        {
            Vector2 targetPosition = CalculateTargetPosition(_targetCollider);
            Vector2 directDirection = (targetPosition - (Vector2)transform.position).normalized;
            float trajectoryAngle = CalculateTrajectoryAngle();
            _launchDirection = (Vector2)(Quaternion.Euler(0f, 0f, trajectoryAngle) * directDirection);
            _dir = _launchDirection;
            _initialTargetDistance = Vector2.Distance(transform.position, targetPosition);
            _homingStartTime = 0f;
        }
        else
        {
            _launchDirection = _facing > 0 ? Vector2.right : Vector2.left;
            _dir = _launchDirection;
            _initialTargetDistance = 0f;
        }

        _initialized = true;
    }

    private float CalculateTrajectoryAngle()
    {
        int profileIndex = CalculateTrajectoryProfileIndex();
        return _trajectorySpread * TrajectoryAngleFactors[profileIndex];
    }

    private int CalculateTrajectoryProfileIndex()
    {
        if (_trajectoryCount == 1)
        {
            return TrajectoryAngleFactors.Length / 2;
        }

        float normalizedIndex = (float)_trajectoryIndex / (_trajectoryCount - 1);
        int profileIndex = Mathf.RoundToInt(normalizedIndex * (TrajectoryAngleFactors.Length - 1));
        return Mathf.Clamp(profileIndex, 0, TrajectoryAngleFactors.Length - 1);
    }

    private Vector2 CalculateTargetPosition(Collider2D targetCollider)
    {
        int profileIndex = CalculateTrajectoryProfileIndex();
        Bounds bounds = targetCollider.bounds;
        float heightOffset = bounds.extents.y * TargetHeightFactors[profileIndex];
        return (Vector2)bounds.center + Vector2.up * heightOffset;
    }

    private Collider2D FindNearestEnemy(IDamageable ignoredDamageable)
    {
        var filter = new ContactFilter2D();
        filter.SetLayerMask(_enemyLayer);
        filter.useTriggers = true;

        Vector2 origin = PlayerPosition + _sightOffset;
        int count = Physics2D.OverlapCircle(origin, _searchRadius, filter, Hits);

        Collider2D nearest = null;
        float minSqr = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider2D col = Hits[i];
            if (!EnemyHurtbox.TryGetDamageTarget(col, out IDamageable damageable) ||
                ReferenceEquals(damageable, ignoredDamageable))
            {
                continue;
            }

            Vector2 enemyPos = col.bounds.center;
            float sqr = (enemyPos - origin).sqrMagnitude;

            if (sqr >= minSqr)
            {
                continue;
            }

            if (!HasLineOfSight(origin, enemyPos))
            {
                continue;
            }

            minSqr = sqr;
            nearest = col;
        }

        return nearest;
    }

    private void SetTarget(Collider2D targetCollider)
    {
        StopTrackingTarget();
        _targetCollider = targetCollider;
        if (_targetCollider == null)
        {
            return;
        }

        EnemyHurtbox.TryGetDamageTarget(_targetCollider, out _targetDamageable);
        _targetHealth = _targetCollider.GetComponentInParent<EnemyHealth>();
        if (_targetHealth != null)
        {
            _targetHealth.OnDeath += HandleTargetDeath;
        }
    }

    private void StopTrackingTarget()
    {
        if (_targetHealth != null)
        {
            _targetHealth.OnDeath -= HandleTargetDeath;
        }

        _targetCollider = null;
        _targetDamageable = null;
        _targetHealth = null;
    }

    private void HandleTargetDeath()
    {
        Retarget(_targetDamageable);
    }

    private void Retarget(IDamageable ignoredDamageable)
    {
        SetTarget(FindNearestEnemy(ignoredDamageable));
        if (_targetCollider == null)
        {
            return;
        }

        Vector2 targetPosition = CalculateTargetPosition(_targetCollider);
        _launchDirection = _dir;
        _initialTargetDistance = Vector2.Distance(transform.position, targetPosition);
        _homingStartTime = _t;
    }

    private bool HasLineOfSight(Vector2 from, Vector2 to)
    {
        RaycastHit2D hit = Physics2D.Linecast(from, to, _obstacleLayer);
        bool visible = hit.collider == null;

        if (_drawDebug)
        {
            Debug.DrawLine(from, to, visible ? Color.green : Color.red, 1f);
        }

        return visible;
    }

    private float CalculateLaunchProgress(Vector2 targetCenter)
    {
        float timeProgress = CalculateTimeProgress();
        if (_initialTargetDistance <= PositionEpsilon)
        {
            return timeProgress;
        }

        float currentDistance = Vector2.Distance(_rigidbody2D.position, targetCenter);
        float distanceProgress = 1f - currentDistance / _initialTargetDistance;
        return Mathf.Max(timeProgress, Mathf.Clamp01(distanceProgress));
    }

    private float CalculateTimeProgress()
    {
        return Mathf.Clamp01(_t / _accelerationDuration);
    }

    private float CalculateHomingProgress()
    {
        int profileIndex = CalculateTrajectoryProfileIndex();
        float duration = _homingDuration * HomingDurationFactors[profileIndex];
        float normalizedTime = Mathf.Clamp01((_t - _homingStartTime) / duration);
        return Mathf.SmoothStep(0f, 1f, normalizedTime);
    }

    private void ApplyVelocity(float launchProgress)
    {
        float speedMultiplier = Mathf.Max(0f, _speedCurve.Evaluate(launchProgress));
        _rigidbody2D.linearVelocity = _dir * Speed * speedMultiplier;

        float angle = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void OnDisable()
    {
        if (_explodeRoutine != null)
        {
            StopCoroutine(_explodeRoutine);
            _explodeRoutine = null;
        }

        _isExploding = false;
        _initialized = false;
        StopTrackingTarget();
        _launchDirection = Vector2.zero;
        _initialTargetDistance = 0f;
        _homingStartTime = 0f;
        _explosionTarget = null;
        _explosionLocalPosition = Vector3.zero;
        _facing = 1;
        _trajectoryIndex = 0;
        _trajectoryCount = 1;

        if (_collider2D != null)
        {
            _collider2D.enabled = true;
        }

        if (_rigidbody2D != null)
        {
            _rigidbody2D.linearVelocity = Vector2.zero;
        }

    }
}
