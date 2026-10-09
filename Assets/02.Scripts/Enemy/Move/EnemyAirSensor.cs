using UnityEngine;

/// <summary>
/// 공중 몬스터의 지형 장애물(벽, 천장, 바닥) 및 이동 영역 경계, 플레이어 시야 차폐를 감지하는 센서 컴포넌트.
/// </summary>
[DisallowMultipleComponent]
public class EnemyAirSensor : MonoBehaviour
{
    private const int CastCapacity = 16;
    [Header("장애물 감지 거리 설정")]
    [SerializeField, Min(0.1f)] private float _horizontalCheckDistance = 1.2f;
    [SerializeField, Min(0.1f)] private float _verticalCheckDistance = 1.0f;
    [SerializeField] private LayerMask _obstacleMask = Constants.Layers.GroundMask;

    private readonly RaycastHit2D[] _hits = new RaycastHit2D[CastCapacity];
    private ContactFilter2D _obstacleFilter;
    private Collider2D _bodyCollider;
    private Rigidbody2D _rigidbody;

    public float HorizontalCheckDistance => _horizontalCheckDistance;
    public float VerticalCheckDistance => _verticalCheckDistance;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (_bodyCollider != null && _rigidbody != null)
        {
            return;
        }

        _obstacleFilter.SetLayerMask(_obstacleMask);
        _obstacleFilter.useTriggers = false;

        _bodyCollider = GetComponent<Collider2D>();
        _rigidbody = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// 지정한 방향과 거리에 지형 장애물이 존재하는지 검사합니다.
    /// </summary>
    public bool HasObstacleInDirection(Vector2 direction, float distance)
    {
        EnsureInitialized();

        if (_bodyCollider == null)
        {
            return false;
        }

        Vector2 origin = _bodyCollider.bounds.center;
        int hitCount = Physics2D.Raycast(origin, direction.normalized, _obstacleFilter, _hits, distance);
        return hitCount > 0;
    }

    /// <summary>몸체 전체를 검사하여 지형과 충돌하지 않는 이동 거리를 반환합니다.</summary>
    public float CalculateAllowedTravelDistance(Vector2 direction, float distance, float skin)
    {
        EnsureInitialized();
        if (_bodyCollider == null)
        {
            return 0f;
        }

        int hitCount = _bodyCollider.Cast(direction.normalized, _obstacleFilter, _hits, distance + skin);
        if (hitCount == _hits.Length)
        {
            return 0f;
        }

        float allowedDistance = distance;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = _hits[i];
            if (hit.collider.gameObject.layer == Constants.Layers.OneWayPlatform &&
                (direction.y >= 0f || hit.normal.y <= 0f || _bodyCollider.bounds.min.y < hit.collider.bounds.max.y - skin))
            {
                continue;
            }
            allowedDistance = Mathf.Min(allowedDistance, Mathf.Max(0f, hit.distance - skin));
        }
        return allowedDistance;
    }

    /// <summary>현재 위치와 무관하게 예정된 직선 경로 전체를 몸체 크기로 검사합니다.</summary>
    public bool IsFlightPathClear(Vector2 start, Vector2 end, float skin)
    {
        EnsureInitialized();
        if (_bodyCollider == null)
        {
            return false;
        }
        Bounds bounds = _bodyCollider.bounds;
        Vector2 origin = start + ((Vector2)bounds.center - (Vector2)transform.position);
        Vector2 movement = end - start;
        int hitCount = Physics2D.BoxCast(origin, bounds.size, 0f, movement.normalized, _obstacleFilter,
            _hits, movement.magnitude + skin);
        if (hitCount == _hits.Length)
        {
            return false;
        }
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = _hits[i];
            if (hit.collider.gameObject.layer == Constants.Layers.OneWayPlatform &&
                (movement.y >= 0f || hit.normal.y <= 0f || origin.y - bounds.extents.y < hit.collider.bounds.max.y - skin))
            {
                continue;
            }
            return false;
        }
        return true;
    }

    /// <summary>
    /// 목표 위치(플레이어 등)까지의 조준선에 지형 장애물이 가로막혀 있는지 검사합니다.
    /// </summary>
    public bool HasLineOfSight(Vector2 targetPosition)
    {
        EnsureInitialized();

        if (_bodyCollider == null)
        {
            return true;
        }

        Vector2 origin = _bodyCollider.bounds.center;
        Vector2 difference = targetPosition - origin;
        float distance = difference.magnitude;

        if (distance < 0.05f)
        {
            return true;
        }

        int hitCount = Physics2D.Raycast(origin, difference.normalized, _obstacleFilter, _hits, distance);
        for (int i = 0; i < hitCount; i++)
        {
            if (_hits[i].collider != null && !_hits[i].collider.isTrigger && _hits[i].collider != _bodyCollider)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 이동 영역 및 지형 장애물을 고려하여 지정 방향으로 안전하게 비행할 수 있는지 검사합니다.
    /// </summary>
    public bool CanFlyAhead(EnemyMoveArea moveArea, Vector2 currentPos, Vector2 direction, float distance)
    {
        if (moveArea != null)
        {
            Vector2 nextPos = currentPos + direction.normalized * distance;
            if (!moveArea.IsPositionInArea(nextPos))
            {
                return false;
            }
        }

        return !HasObstacleInDirection(direction, distance);
    }
}
