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

    private Vector2 _deltaPosition;
    public Vector2 DeltaPosition => _deltaPosition;

    private bool _isValid;

    private void Start()
    {
        _isValid = HasRequiredReferences();

        if (!_isValid) return;

        _platformBody.position = _points[0].position;
    }

    private void FixedUpdate()
    {
        _velocity = Vector2.zero;
        _deltaPosition = Vector2.zero;

        if (!_isValid) return;

        if (_waitTimer > 0f)
        {
            _waitTimer -= Time.fixedDeltaTime;
            return;
        }

        Vector2 currentPosition = _platformBody.position;
        Vector2 targetPosition = _points[_targetIndex].position;

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

        _deltaPosition = nextPosition - currentPosition;
        _velocity = _deltaPosition / Time.fixedDeltaTime;
        _platformBody.MovePosition(nextPosition);
    }

    public void OnPlatformCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.TryGetComponent<Player>(out Player player))
        {
            ContactPoint2D contact = collision.GetContact(0);
            if (contact.normal.y < -0.5f)
            {
                player.Movement.SetMovingPlatform(this);
            }
        }
    }

    public void OnPlatformCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.TryGetComponent<Player>(out Player player))
        {
            player.Movement.SetMovingPlatform(null);
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

    private bool HasRequiredReferences()
    {
        if (_platformBody == null)
        {
            Debug.LogWarning($"{name}: Platform Body 참조가 없습니다.", this);
            return false;
        }

        if (_points == null || _points.Length < 2)
        {
            Debug.LogWarning($"{name}: 이동 경로에는 Point가 2개 이상 필요합니다.", this);
            return false;
        }

        for (int i = 0; i < _points.Length; i++)
        {
            if (_points[i] != null)
            {
                continue;
            }

            Debug.LogWarning($"{name}: Points의 {i}번 참조가 없습니다.", this);
            return false;
        }

        return true;
    }
}
