using System.Collections.Generic;
using UnityEngine;

public enum MovingPlatformMode
{
    Loop,
    SingleTrip,
    PassengerControlled,
    SwitchControlled
}

public enum MovingPlatformEndpoint
{
    Start,
    Destination
}

[DefaultExecutionOrder(-100)]
public class MovingPlatform : MonoBehaviour
{
    private enum MovementState
    {
        IdleAtStart,
        IdleAtDestination,
        MovingForward,
        WaitingAtDestination,
        MovingBackward,
        Completed
    }

    [SerializeField] private Rigidbody2D _platformBody;
    [SerializeField] private Transform[] _points;
    [SerializeField] private MovingPlatformMode _mode = MovingPlatformMode.Loop;
    [SerializeField, Min(0f)] private float _speed = 2f;
    [SerializeField, Min(0f)] private float _waitTime = 0.1f;

    [SerializeField, ReadOnly] private MovementState _state;

    private Vector2 _velocity;
    public Vector2 Velocity => _velocity;

    private Vector2 _deltaPosition;
    public Vector2 DeltaPosition => _deltaPosition;

    private int _targetIndex = 1;
    private float _waitTimer;
    private bool _isValid;
    private GameAudio.EventAudio _audio;
    private bool _audioMoving;

    private void UpdateAudio(bool moving, bool completed)
    {
        if (moving == _audioMoving) return;
        if (_audio == null) _audio = GameAudio.EventAudio.For(this, "Environment");
        _audioMoving = moving;
        if (moving)
        {
            _audio.Begin();
            _audio.Play("Environment.PlatformStart");
            _audio.Play("Environment.PlatformLoop");
        }
        else
        {
            if (completed) _audio.Complete();
            else _audio.Cancel();
            if (completed) AudioManager.Instance?.PlaySceneEvent("Environment.PlatformStop", this);
        }
    }

    private void OnDisable() => UpdateAudio(false, false);

    private readonly HashSet<Collider2D> _passengerColliders =
        new HashSet<Collider2D>();

    private void Start()
    {
        _isValid = HasRequiredReferences();

        if (!_isValid) return;

        ResetPlatform();
    }

    private void FixedUpdate()
    {
        _velocity = Vector2.zero;
        _deltaPosition = Vector2.zero;

        if (!_isValid || !IsMovingState())
        {
            UpdateAudio(false, false);
            return;
        }

        if (_waitTimer > 0f)
        {
            _waitTimer -= Time.fixedDeltaTime;
            return;
        }

        UpdateAudio(_speed > 0f, false);
        MoveToTarget();
    }

    public bool TryMoveToEndpoint(MovingPlatformEndpoint endpoint)
    {
        if (!_isValid || _mode != MovingPlatformMode.SwitchControlled)
        {
            return false;
        }

        if (endpoint == MovingPlatformEndpoint.Destination &&
            _state == MovementState.IdleAtStart)
        {
            StartMovingForward();
            return true;
        }

        if (endpoint == MovingPlatformEndpoint.Start &&
            _state == MovementState.IdleAtDestination)
        {
            StartMovingBackward();
            return true;
        }

        return false;
    }

    public void ResetPlatform()
    {
        if (!_isValid) return;

        UpdateAudio(false, false);
        _platformBody.position = _points[0].position;
        _targetIndex = 1;
        _waitTimer = 0f;
        _velocity = Vector2.zero;
        _deltaPosition = Vector2.zero;
        _state = _mode == MovingPlatformMode.Loop
            ? MovementState.MovingForward
            : MovementState.IdleAtStart;
    }

    public void OnPlatformCollisionEnter2D(Collision2D collision)
    {
        Player player = collision.collider.GetComponentInParent<Player>();
        if (player == null || !IsStandingOnPlatform(collision))
        {
            return;
        }

        player.Movement.SetMovingPlatform(this);

        if (_passengerColliders.Add(collision.collider) &&
            _passengerColliders.Count == 1)
        {
            HandlePassengerEntered();
        }
    }

    public void OnPlatformCollisionExit2D(Collision2D collision)
    {
        if (!_passengerColliders.Remove(collision.collider))
        {
            return;
        }

        if (_passengerColliders.Count > 0)
        {
            return;
        }

        Player player = collision.collider.GetComponentInParent<Player>();
        if (player != null)
        {
            player.Movement.SetMovingPlatform(null);
        }

        HandlePassengerExited();
    }

    private void HandlePassengerEntered()
    {
        if (_state == MovementState.IdleAtStart &&
            (_mode == MovingPlatformMode.SingleTrip ||
                _mode == MovingPlatformMode.PassengerControlled))
        {
            StartMovingForward();
        }
    }

    private void HandlePassengerExited()
    {
        if (_mode != MovingPlatformMode.PassengerControlled)
        {
            return;
        }

        if (_state == MovementState.MovingForward)
        {
            StartReturningFromCurrentSegment();
        }
        else if (_state == MovementState.WaitingAtDestination)
        {
            StartMovingBackward();
        }
    }

    private void MoveToTarget()
    {
        Vector2 currentPosition = _platformBody.position;
        Vector2 targetPosition = _points[_targetIndex].position;

        var nextPosition = Vector2.MoveTowards(
            currentPosition,
            targetPosition,
            _speed * Time.fixedDeltaTime
        );

        _deltaPosition = nextPosition - currentPosition;
        _velocity = _deltaPosition / Time.fixedDeltaTime;
        _platformBody.MovePosition(nextPosition);

        if (nextPosition == targetPosition)
        {
            ReachTarget();
        }
    }

    private void ReachTarget()
    {
        UpdateAudio(false, true);
        if (_state == MovementState.MovingForward)
        {
            ReachForwardTarget();
        }
        else if (_state == MovementState.MovingBackward)
        {
            ReachBackwardTarget();
        }
    }

    private void ReachForwardTarget()
    {
        if (_targetIndex < _points.Length - 1)
        {
            _targetIndex++;
            _waitTimer = _waitTime;
            return;
        }

        if (_mode == MovingPlatformMode.Loop)
        {
            StartMovingBackward();
            _waitTimer = _waitTime;
        }
        else if (_mode == MovingPlatformMode.PassengerControlled)
        {
            _state = MovementState.WaitingAtDestination;
        }
        else if (_mode == MovingPlatformMode.SwitchControlled)
        {
            _state = MovementState.IdleAtDestination;
        }
        else
        {
            _state = MovementState.Completed;
        }
    }

    private void ReachBackwardTarget()
    {
        if (_targetIndex > 0)
        {
            _targetIndex--;
            _waitTimer = _waitTime;
            return;
        }

        if (_mode == MovingPlatformMode.Loop)
        {
            StartMovingForward();
            _waitTimer = _waitTime;
        }
        else if (_mode == MovingPlatformMode.PassengerControlled &&
            _passengerColliders.Count > 0)
        {
            StartMovingForward();
        }
        else
        {
            _targetIndex = 1;
            _state = MovementState.IdleAtStart;
        }
    }

    private void StartMovingForward()
    {
        _targetIndex = 1;
        _waitTimer = 0f;
        _state = MovementState.MovingForward;
    }

    private void StartMovingBackward()
    {
        _targetIndex = _points.Length - 2;
        _waitTimer = 0f;
        _state = MovementState.MovingBackward;
    }

    private void StartReturningFromCurrentSegment()
    {
        _targetIndex = Mathf.Max(0, _targetIndex - 1);
        _waitTimer = 0f;
        _state = MovementState.MovingBackward;
    }

    private bool IsMovingState()
    {
        return _state == MovementState.MovingForward ||
            _state == MovementState.MovingBackward;
    }

    private bool IsStandingOnPlatform(Collision2D collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y < -0.5f)
            {
                return true;
            }
        }

        return false;
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
