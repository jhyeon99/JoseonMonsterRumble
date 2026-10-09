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

    [Header("절벽 안전 감지 설정")]
    [SerializeField, Min(0f)] private float _edgeMargin = 0.1f;
    [SerializeField, Min(0.01f)] private float _groundProbeDepth = 0.3f;

    private readonly ContactPoint2D[] _groundContacts = new ContactPoint2D[8];
    private readonly RaycastHit2D[] _groundHits = new RaycastHit2D[8];
    private ContactFilter2D _groundFilter;
    private Collider2D _bodyCollider;
    private Rigidbody2D _rigidbody;

    public float EdgeMargin => _edgeMargin;
    public float GroundProbeDepth => _groundProbeDepth;

    private void Awake()
    {
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

            return true;
        }

        return false;
    }

    /// <summary>
    /// 지정 속도와 제동 거리를 감안하여 전방으로 안전하게 전진 가능한지 검사합니다.
    /// 절벽(바닥 없음)이나 이동 영역 초과 시 false를 반환합니다.
    /// </summary>
    public bool CanMoveAhead(EnemyMoveArea moveArea, float speed, float lookAhead)
    {
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

        int count = Physics2D.BoxCast(
            origin,
            Vector2.one * ProbeSize,
            0f,
            Vector2.down,
            _groundFilter,
            _groundHits,
            ProbeLift + _groundProbeDepth);

        for (int i = 0; i < count; i++)
        {
            if (_groundHits[i].normal.y > GroundNormalThreshold)
            {
                return true;
            }
        }

        return false;
    }
}
