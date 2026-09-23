using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    [SerializeField] private Rigidbody2D _platformBody;
    [SerializeField] private Transform[] _points;
    [SerializeField, Min(0f)] private float _speed = 2f;

    private int _targetIndex = 1;
    private int _direction = 1;

    private void Start()
    {
        if (!IsValid())
            return;

        _platformBody.position = _points[0].position;
    }

    private void FixedUpdate()
    {
        if (!IsValid())
            return;

        var targetPosition = (Vector2)_points[_targetIndex].position;

        var nextPosition = Vector2.MoveTowards(
            _platformBody.position,
            targetPosition,
            _speed * Time.fixedDeltaTime
        );

        _platformBody.MovePosition(nextPosition);

        if (Vector2.Distance(nextPosition, targetPosition) <= 0.01f)
        {
            UpdateTarget();
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