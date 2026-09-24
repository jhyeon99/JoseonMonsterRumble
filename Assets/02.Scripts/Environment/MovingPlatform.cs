using UnityEngine;

[DefaultExecutionOrder(-100)]
public class MovingPlatform : MonoBehaviour
{
    [SerializeField] private Rigidbody2D _platformBody;
    [SerializeField] private Transform[] _points;
    [SerializeField, Min(0f)] private float _speed = 2f;
    [SerializeField, Min(0f)] private float _waitTime = 0.1f;

    private Vector2 _velocity;
    public Vector2 Velocity => _velocity;
    private int _targetIndex = 1;
    private int _direction = 1;
    private float _waitTimer = 0f;

    private void Start()
    {
        if (!IsValid()) return;

        _platformBody.position = _points[0].position;
    }

    private void FixedUpdate()
    {
        _velocity = Vector2.zero;

        if (!IsValid()) return;

        // [추가] 대기 중일 때는 이동 연산 중단 (velocity = Vector2.zero)
        if (_waitTimer > 0f)
        {
            _waitTimer -= Time.fixedDeltaTime;
            return;
        }

        Vector2 currentPosition = _platformBody.position;
        Vector2 targetPosition = _points[_targetIndex].position;

        // [수정] 이동 연산 전에 미리 도착 여부를 확인하여 targetPosition 및 대기 타이머 갱신
        if (Vector2.Distance(currentPosition, targetPosition) <= 0.01f)
        {
            UpdateTarget();
            _waitTimer = _waitTime;
            return;
        }

        var nextPosition = Vector2.MoveTowards(
            currentPosition,
            targetPosition,
            _speed * Time.fixedDeltaTime
        );

        _velocity = (nextPosition - currentPosition) / Time.fixedDeltaTime;
        _platformBody.MovePosition(nextPosition);
    }

    public void OnPlatformCollisionEnter2D(Collision2D collision)
    {   
        if (collision.gameObject.TryGetComponent<Player>(out var player))
        {
            ContactPoint2D contact = collision.GetContact(0);
            if (contact.normal.y < -0.5f)
            {
                player.transform.SetParent(transform);
            }
        }
    }

    public void OnPlatformCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.TryGetComponent<Player>(out var player))
        {
            player.transform.SetParent(null);
        }
    }

    private void UpdateTarget()
    {
        if (_targetIndex == _points.Length - 1)
        {
            _direction = -1;
        }
        else if (_targetIndex == 0)
        {
            _direction = 1;
        }

        _targetIndex += _direction;
    }

    private bool IsValid()
    {
        return _platformBody != null
               && _points != null
               && _points.Length >= 2;
    }
}