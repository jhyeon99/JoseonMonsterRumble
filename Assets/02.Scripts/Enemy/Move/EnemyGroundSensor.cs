using UnityEngine;

/// <summary>
/// 지상 적의 접지 상태와 전방 절벽(낭떠러지) 및 이동 영역 경계를 감지하는 센서 컴포넌트.
/// </summary>
[DisallowMultipleComponent]
public class EnemyGroundSensor : MonoBehaviour
{
    private const float GroundNormalThreshold = 0.7f;
    private const float ProbeSize = 0.05f;
    private const float ProbeLift = 0.1f;
    private const int EdgeSearchIterations = 8;

    [Header("절벽 안전 감지 설정")]
    [SerializeField, Min(0f)] private float _edgeMargin = 0.1f;
    [SerializeField, Min(0.01f)] private float _groundProbeDepth = 0.3f;

    private readonly ContactPoint2D[] _groundContacts = new ContactPoint2D[8];
    private readonly RaycastHit2D[] _groundHits = new RaycastHit2D[8];
    private ContactFilter2D _groundFilter;
    private Collider2D _bodyCollider;
    private Rigidbody2D _rigidbody;
    private float _supportedBottomY;
    private bool _hasSupportedBottom;

    public float EdgeMargin => _edgeMargin;
    public float GroundProbeDepth => _groundProbeDepth;

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

        _groundFilter.SetLayerMask(Constants.Layers.GroundAllMask);
        _groundFilter.useTriggers = false;

        _bodyCollider = GetComponent<Collider2D>();
        _rigidbody = GetComponent<Rigidbody2D>();

        if (_bodyCollider == null || _bodyCollider.isTrigger)
        {
            Debug.LogError("EnemyGroundSensor: 루트에 몸체용 비트리거 Collider2D가 필요합니다.", this);
        }
    }

    /// <summary>
    /// 바닥 접촉 여부를 검사합니다. (바닥 방향 접촉 및 실질적 지지 여부)
    /// </summary>
    public bool IsGrounded()
    {
        EnsureInitialized();

        if (_rigidbody == null)
        {
            return false;
        }

        int count = _rigidbody.GetContacts(_groundFilter, _groundContacts);
        for (int i = 0; i < count; i++)
        {
            ContactPoint2D contact = _groundContacts[i];
            if (contact.normal.y <= GroundNormalThreshold) continue;
            if (contact.normalImpulse <= 0f) continue;

            if (_bodyCollider != null)
            {
                _supportedBottomY = _bodyCollider.bounds.min.y;
                _hasSupportedBottom = true;
            }
            return true;
        }

        return false;
    }

    /// <summary>떠오르기 전 지면 높이에서 경로를 검사하여 절벽을 넘지 않는 수평 속도를 반환한다.</summary>
    public float CalculateSafeKnockbackSpeed(float speed)
    {
        EnsureInitialized();
        IsGrounded();
        if (speed == 0f || !_hasSupportedBottom || _bodyCollider == null || _rigidbody == null)
        {
            return 0f;
        }

        Bounds bounds = _bodyCollider.bounds;
        float direction = Mathf.Sign(speed);
        float frontX = bounds.center.x + direction * (bounds.extents.x + _edgeMargin);
        float distance = Mathf.Abs(speed) * Time.fixedDeltaTime;
        float safeDistance = 0f;

        // 끝점뿐 아니라 경로도 검사하여 강한 넉백으로 좁은 틈을 건너가지 않게 한다.
        while (safeDistance < distance)
        {
            float nextDistance = Mathf.Min(safeDistance + ProbeSize, distance);
            if (HasGroundBelow(frontX + direction * nextDistance, _supportedBottomY))
            {
                safeDistance = nextDistance;
                continue;
            }

            float blockedDistance = nextDistance;
            for (int i = 0; i < EdgeSearchIterations; i++)
            {
                float middleDistance = (safeDistance + blockedDistance) * 0.5f;
                if (HasGroundBelow(frontX + direction * middleDistance, _supportedBottomY))
                {
                    safeDistance = middleDistance;
                }
                else
                {
                    blockedDistance = middleDistance;
                }
            }
            break;
        }

        return direction * safeDistance / Time.fixedDeltaTime;
    }

    private bool HasGroundBelow(float x, float bottomY)
    {
        var origin = new Vector2(x, bottomY + ProbeLift);
        return HasGroundBelow(origin);
    }

    private bool HasGroundBelow(Vector2 origin)
    {
        int count = Physics2D.BoxCast(origin, Vector2.one * ProbeSize, 0f, Vector2.down,
            _groundFilter, _groundHits, ProbeLift + _groundProbeDepth);
        for (int i = 0; i < count; i++)
        {
            if (_groundHits[i].normal.y > GroundNormalThreshold)
            {
                return true;
            }
        }
        return false;
    }

    private void OnDisable()
    {
        _hasSupportedBottom = false;
        _supportedBottomY = 0f;
    }

    /// <summary>
    /// 지정 속도와 제동 거리를 감안하여 전방으로 안전하게 전진 가능한지 검사합니다.
    /// 절벽(바닥 없음)이나 이동 영역 초과 시 false를 반환합니다.
    /// </summary>
    public bool CanMoveAhead(EnemyMoveArea moveArea, float speed, float lookAhead)
    {
        EnsureInitialized();

        if (_bodyCollider == null || _rigidbody == null)
        {
            return false;
        }

        float direction = Mathf.Sign(speed);
        if (moveArea != null)
        {
            float nextX = _rigidbody.position.x + direction * lookAhead;
            float currentExcess = Mathf.Abs(_rigidbody.position.x - moveArea.ClampX(_rigidbody.position.x));
            float nextExcess = Mathf.Abs(nextX - moveArea.ClampX(nextX));
            if (nextExcess > currentExcess)
            {
                return false;
            }
        }

        Bounds bounds = _bodyCollider.bounds;
        var origin = new Vector2(
            bounds.center.x + direction * (bounds.extents.x + _edgeMargin + lookAhead),
            bounds.min.y + ProbeLift);

        return HasGroundBelow(origin);
    }
}
