using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class GroundProjectile : Projectile
{
    private const float OneWaySurfaceTolerance = 0.01f;
    private const float GroundNormalThreshold = 0.5f;
    private const int CastCapacity = 64;

    [Header("포물선")]
    [Tooltip("아래로 가속되는 정도. 높일수록 곡선의 상승과 낙하가 빨라집니다.")]
    [SerializeField, Min(0.01f)] private float _gravityAcceleration = 35f;
    [Tooltip("출발점과 목표점 중 높은 쪽보다 최소한 얼마나 위로 솟을지")]
    [SerializeField, Min(0.01f)] private float _arcHeight = 1.0f;

    private readonly RaycastHit2D[] _hits = new RaycastHit2D[CastCapacity];
    private CircleCollider2D _circle;
    private TrailRenderer _trail;
    private ContactFilter2D _castFilter;
    private Vector2 _velocity;

    protected override Vector2 HitDirection => IsReflected ? ReflectDirection : _velocity.normalized;

    protected override void Awake()
    {
        base.Awake();
        _circle = GetComponent<CircleCollider2D>();
        _trail = GetComponentInChildren<TrailRenderer>();
        _castFilter.useTriggers = true;
    }

    public override void OnSpawn(Transform muzzlePoint, Transform playerPoint, float damageIncreaseValue)
    {
        if (muzzlePoint == null || playerPoint == null || Speed <= 0f ||
            _gravityAcceleration <= 0f || _arcHeight <= 0f)
        {
            Debug.LogError("GroundProjectile: 총구·플레이어와 양수 속도·중력·포물선 높이가 필요합니다.", this);
            gameObject.SetActive(false);
            return;
        }
        Collider2D targetBody = playerPoint.GetComponent<Collider2D>();
        if (targetBody == null || targetBody.isTrigger)
        {
            Debug.LogError("GroundProjectile: 플레이어 몸체 Collider2D가 필요합니다.", this);
            gameObject.SetActive(false);
            return;
        }

        base.OnSpawn(muzzlePoint, playerPoint, damageIncreaseValue);
        transform.position = muzzlePoint.position;
        Vector2 offset = (Vector2)targetBody.bounds.center - MuzzlePosition;
        _velocity = CalculateLaunchVelocity(offset);
        if (_trail != null)
        {
            _trail.Clear();
        }
    }

    private Vector2 CalculateLaunchVelocity(Vector2 offset)
    {
        // 가까운 목표에서도 양 끝점보다 위로 솟는 구간을 확보한다.
        // 수평 속도는 상한으로 사용하며, 짧은 거리에서는 곡선을 위해 비행 시간을 늘린다.
        float minimumPeak = Mathf.Max(0f, offset.y) + _arcHeight;
        float upwardSpeed = Mathf.Sqrt(2f * _gravityAcceleration * minimumPeak);
        float descentSpeed = Mathf.Sqrt(2f * _gravityAcceleration * (minimumPeak - offset.y));
        float arcFlightTime = (upwardSpeed + descentSpeed) / _gravityAcceleration;
        float flightTime = Mathf.Max(Mathf.Abs(offset.x) / Speed, arcFlightTime);
        return new Vector2(offset.x / flightTime,
            offset.y / flightTime + _gravityAcceleration * flightTime * 0.5f);
    }

    protected override void Update()
    {
        ReturnProjectile();
    }

    private void FixedUpdate()
    {
        if (IsReflected)
        {
            MoveReflected(Time.fixedDeltaTime);
        }
        else
        {
            Fire();
        }
    }

    protected override void Fire()
    {
        if (!_isLaunching)
        {
            return;
        }
        float step = Time.fixedDeltaTime;
        Vector2 acceleration = Vector2.down * _gravityAcceleration;
        Vector2 displacement = _velocity * step + acceleration * (step * step * 0.5f);
        MoveProjectile(displacement);
        if (!gameObject.activeSelf || IsReflected)
        {
            return;
        }
        _velocity += acceleration * step;
        float angle = Mathf.Atan2(_velocity.y, _velocity.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    protected override void MoveProjectile(Vector2 displacement)
    {
        float distance = displacement.magnitude;
        if (distance <= 0f)
        {
            return;
        }
        Vector2 origin = transform.TransformPoint(_circle.offset);
        Vector3 scale = transform.lossyScale;
        float radius = _circle.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        _castFilter.SetLayerMask(Constants.Layers.GroundAllMask |
            (IsReflected ? Constants.Layers.EnemyMask : Constants.Layers.PlayerMask));
        int count = Physics2D.CircleCast(origin, radius, displacement / distance,
            _castFilter, _hits, distance);
        if (count == _hits.Length)
        {
            Debug.LogWarning("GroundProjectile: 충돌 검사 버퍼가 가득 찼습니다. 판정 누락 방지를 위해 반환합니다.", this);
            gameObject.SetActive(false);
            return;
        }

        Vector3 startPosition = transform.position;
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = _hits[i];
            Collider2D other = hit.collider;
            int layer = other.gameObject.layer;
            Vector3 hitPosition = startPosition + (Vector3)(displacement / distance * hit.distance);
            if ((Constants.Layers.GroundAllMask & (1 << layer)) != 0)
            {
                if (other.isTrigger || (IsReflected && ShouldIgnoreReturnGround))
                {
                    continue;
                }
                if (layer == Constants.Layers.OneWayPlatform &&
                    (displacement.y >= 0f || hit.normal.y < GroundNormalThreshold ||
                     origin.y - radius < other.bounds.max.y - OneWaySurfaceTolerance))
                {
                    continue;
                }
                transform.position = hitPosition;
                gameObject.SetActive(false);
                return;
            }

            if (IsReflected)
            {
                if (!EnemyHurtbox.TryGetDamageTarget(other, out IDamageable reflectedTarget))
                {
                    continue;
                }
                transform.position = hitPosition;
                ApplyReflectedHit(reflectedTarget, hit.point);
                if (!gameObject.activeSelf)
                {
                    return;
                }
                continue;
            }
            if (other.GetComponentInParent<Player>() == null)
            {
                continue;
            }
            IDamageable damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null)
            {
                continue;
            }
            if (damageable is PlayerHealth playerHealth && playerHealth.IsInvincible)
            {
                NotifyInvincibleHit(playerHealth, hit.point);
                continue;
            }
            transform.position = hitPosition;
            ApplyHit(damageable, hit.point);
            return;
        }
        transform.position = startPosition + (Vector3)displacement;
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        // 목객 돌의 판정은 이동 구간 Cast만 소유한다. Trigger 콜백의 이중 피해를 막는다.
    }

    private void OnDisable()
    {
        _velocity = Vector2.zero;
        _isLaunching = false;
    }
}
