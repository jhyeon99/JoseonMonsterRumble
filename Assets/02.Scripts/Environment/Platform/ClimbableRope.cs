using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class ClimbableRope : MonoBehaviour
{
    [Header("등반 범위")]
    [SerializeField] private Transform _topPoint;
    [SerializeField] private Transform _bottomPoint;

    [Header("상단 도착 위치")]
    [SerializeField] private Transform _topExitPoint;

    public float CenterX => transform.position.x;
    public float TopY => _topPoint.position.y;
    public float BottomY => _bottomPoint.position.y;
    public Vector2 TopExitPosition => _topExitPoint.position;

    private void Awake()
    {
        if (_topPoint == null ||
            _bottomPoint == null ||
            _topExitPoint == null)
        {
            Debug.LogError(
                "ClimbableRope의 Top Point, Bottom Point, Top Exit Point를 연결하세요.",
                this);

            enabled = false;
            return;
        }

        BoxCollider2D trigger = GetComponent<BoxCollider2D>();

        if (!trigger.isTrigger)
        {
            Debug.LogWarning(
                "ClimbableRope의 BoxCollider2D는 Is Trigger를 활성화하세요.",
                this);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Player player = other.GetComponentInParent<Player>();

        if (player == null)
        {
            return;
        }

        player.EnterRopeRange(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        Player player = other.GetComponentInParent<Player>();

        if (player == null)
        {
            return;
        }

        player.ExitRopeRange(this);
    }

    private void Reset()
    {
        BoxCollider2D trigger = GetComponent<BoxCollider2D>();
        trigger.isTrigger = true;
    }

    private void OnDrawGizmosSelected()
    {
        if (_topPoint == null || _bottomPoint == null)
        {
            return;
        }

        Gizmos.DrawLine(
            _bottomPoint.position,
            _topPoint.position);

        Gizmos.DrawWireSphere(
            _topPoint.position,
            0.1f);

        Gizmos.DrawWireSphere(
            _bottomPoint.position,
            0.1f);

        if (_topExitPoint == null)
        {
            return;
        }

        Gizmos.DrawWireSphere(
            _topExitPoint.position,
            0.15f);
    }
}