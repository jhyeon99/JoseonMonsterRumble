using UnityEngine;

[DefaultExecutionOrder(-100)]
public class MovingPlatform : MonoBehaviour
{
    [SerializeField] private Rigidbody2D _platformBody;
    [SerializeField] private Transform[] _points;
    [SerializeField, Min(0f)] private float _speed = 2f;

    private Vector2 _velocity;
    private int _targetIndex = 1;
    private int _direction = 1;

    private void Start()
    {
        if (!HasRequiredReferences())
        {
            enabled = false;
            return;
        }

        _platformBody.position = _points[0].position;
    }

    private void FixedUpdate()
    {
        _velocity = Vector2.zero;

        Vector2 currentPosition = _platformBody.position;
        Vector2 targetPosition = _points[_targetIndex].position;

        var nextPosition = Vector2.MoveTowards(
            currentPosition,
            targetPosition,
            _speed * Time.fixedDeltaTime
        );

        _velocity = (nextPosition - currentPosition) / Time.fixedDeltaTime;
        _platformBody.MovePosition(nextPosition);

        if (Vector2.Distance(nextPosition, targetPosition) <= 0.01f)
        {
            UpdateTarget();
        }
    }

    public Vector2 GetVelocity()
    {
        return _velocity;
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
