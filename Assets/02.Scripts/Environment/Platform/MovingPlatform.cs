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
    [SerializeField] private bool _requireItemToMove;
    [SerializeField] private string _requiredItemId = AbilityItemProgress.DokkaebiGamtuId;

    [Header("Passenger 하차 판정")]
    [Tooltip("접촉 종료 직후 남아 있는 착지 판정을 무시하는 시간입니다. 점프 전체의 제한 시간이 아닙니다.")]
    [SerializeField, Min(0f)] private float _passengerExitGraceTime = 0.15f;
    [Tooltip("플레이어 발이 발판 윗면보다 이만큼 아래로 내려가면 떨어진 것으로 판단합니다.")]
    [SerializeField, Min(0f)] private float _passengerDropTolerance = 0.25f;

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
    private Player _passenger;
    private Collider2D _platformCollider;
    private float _passengerFootOffset;
    private float _passengerExitTimer;
    private bool _isCheckingPassengerExit;

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

    private void OnEnable()
    {
        AbilityItemProgress.OnItemAcquired += HandleItemAcquired;
        ItemAcquisitionView.OnItemAcquisitionClosed += HandleItemAcquisitionClosed;
    }

    private void OnDisable()
    {
        ResetPassengerTracking();
        UpdateAudio(false, false);
        AbilityItemProgress.OnItemAcquired -= HandleItemAcquired;
        ItemAcquisitionView.OnItemAcquisitionClosed -= HandleItemAcquisitionClosed;
    }

    private void HandleItemAcquired(AbilityItemData item)
    {
        if (!_requireItemToMove) return;
        if (item != null && !string.IsNullOrEmpty(_requiredItemId) && item.ItemId != _requiredItemId) return;

        if (_passengerColliders.Count > 0 && _state == MovementState.IdleAtStart && !PauseController.IsGameplayInputBlocked)
        {
            StartMovingForward();
        }
    }

    private void HandleItemAcquisitionClosed()
    {
        if (_requireItemToMove && !AbilityItemProgress.HasItem(_requiredItemId))
        {
            return;
        }

        if (_passengerColliders.Count > 0 && _state == MovementState.IdleAtStart)
        {
            StartMovingForward();
        }
    }

    private readonly HashSet<Collider2D> _passengerColliders =
        new HashSet<Collider2D>();

    private void Start()
    {
        _isValid = HasRequiredReferences();

        if (!_isValid) return;

        _platformCollider = _platformBody.GetComponent<Collider2D>();
        ResetPlatform();
    }

    private void FixedUpdate()
    {
        _velocity = Vector2.zero;
        _deltaPosition = Vector2.zero;

        if (_isValid) UpdatePassengerExit();

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

        ResetPassengerTracking();
        UpdateAudio(false, false);
        _platformBody.position = _points[0].position;
        _targetIndex = 1;
        _waitTimer = 0f;
        _velocity = Vector2.zero;
        _deltaPosition = Vector2.zero;
        _state = (_mode == MovingPlatformMode.Loop && !_requireItemToMove)
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
        _passenger = player;
        _passengerFootOffset = collision.collider.bounds.min.y - player.transform.position.y;
        _isCheckingPassengerExit = false;

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
        if (player != null && player.Movement.CurrentPlatform == this)
        {
            player.Movement.SetMovingPlatform(null);
        }

        HandlePassengerExited();
    }

    private void HandlePassengerEntered()
    {
        if (_requireItemToMove && !AbilityItemProgress.HasItem(_requiredItemId))
        {
            return;
        }

        if (_state == MovementState.IdleAtStart &&
            (_mode == MovingPlatformMode.SingleTrip ||
                _mode == MovingPlatformMode.PassengerControlled))
        {
            StartMovingForward();
        }
        else if (_mode == MovingPlatformMode.PassengerControlled && _state == MovementState.MovingBackward)
        {
            // 현재 돌아오던 구간을 반대로 진행한다. 출발점으로 순간 이동하지 않는다.
            _targetIndex = Mathf.Min(_points.Length - 1, _targetIndex + 1);
            _waitTimer = 0f;
            _state = MovementState.MovingForward;
        }
    }

    private void HandlePassengerExited()
    {
        if (_mode != MovingPlatformMode.PassengerControlled)
        {
            return;
        }

        _isCheckingPassengerExit = _state == MovementState.MovingForward ||
            _state == MovementState.WaitingAtDestination;
        _passengerExitTimer = _passengerExitGraceTime;
    }

    private void UpdatePassengerExit()
    {
        if (!_isCheckingPassengerExit || _passengerColliders.Count > 0) return;
        _passengerExitTimer -= Time.fixedDeltaTime;
        if (_passengerExitTimer > 0f) return;

        bool hasLeft = _passenger == null || !_passenger.isActiveAndEnabled || _passenger.Health.IsDead;
        if (!hasLeft)
        {
            bool landedElsewhere = _passenger.IsGrounded && _passenger.Movement.CurrentPlatform != this;
            float platformTop = _platformCollider != null ? _platformCollider.bounds.max.y : _platformBody.position.y;
            float passengerFeet = _passenger.transform.position.y + _passengerFootOffset;
            hasLeft = landedElsewhere || passengerFeet < platformTop - _passengerDropTolerance;
        }
        if (!hasLeft) return;

        _isCheckingPassengerExit = false;
        _passenger = null;
        if (_state == MovementState.MovingForward) StartReturningFromCurrentSegment();
        else if (_state == MovementState.WaitingAtDestination) StartMovingBackward();
    }

    private void ResetPassengerTracking()
    {
        if (_passenger != null && _passenger.Movement.CurrentPlatform == this)
            _passenger.Movement.SetMovingPlatform(null);
        _passenger = null;
        _passengerColliders.Clear();
        _isCheckingPassengerExit = false;
        _passengerExitTimer = 0f;
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
            if (!_requireItemToMove || AbilityItemProgress.HasItem(_requiredItemId))
            {
                StartMovingForward();
            }
            else
            {
                _targetIndex = 1;
                _state = MovementState.IdleAtStart;
            }
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
