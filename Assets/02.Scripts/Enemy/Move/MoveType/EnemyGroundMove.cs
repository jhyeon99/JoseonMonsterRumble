using UnityEngine;

public class EnemyGroundMove : EnemyMoveType
{
    private const float LandingVelocityThreshold = 0.1f;
    private const float PositionTolerance = 0.1f;
    private const int StopCurveSamples = 32;

    [Header("절벽 안전 이동")]
    [SerializeField] private EnemyGroundSensor _groundSensor;
    [SerializeField] private AnimationClip _walkEndClip;

    private float _walkStopTravelTime;
    private float _patrolDirection = 1f;
    private float _pendingPatrolDirection;
    private enum WalkMotionState { Stopped, Moving, Stopping }
    private WalkMotionState _walkMotionState;
    private float _requestedWalkVelocity;
    private float _walkStopVelocity;
    private float _walkStepDistance = float.PositiveInfinity;

    public bool IsWalkStopped => _walkMotionState == WalkMotionState.Stopped &&
        (!Enemy.Data.UseWalkAnimationMotion || Enemy.Animation.GetWalkPhase(out _) == EnemyWalkPhase.None);

    public bool HasWalkFacingControl =>
        (Enemy.StateMachine.CurrentState is IdleState || Enemy.StateMachine.CurrentState is ChaseState ||
         Enemy.StateMachine.CurrentState is ReturnToStartState || Enemy.StateMachine.CurrentState is GroundRangedSpacingState ||
         Enemy.StateMachine.CurrentState is AlertState);

    protected override void Awake()
    {
        base.Awake();

        if (_groundSensor == null)
        {
            TryGetComponent(out _groundSensor);
        }
    }

    private void Start()
    {
        if (!Enemy.Data.UseWalkAnimationMotion)
        {
            return;
        }
        if (_walkEndClip == null)
        {
            Debug.LogError("EnemyGroundMove: Walk End Clip을 연결해야 합니다.", this);
            return;
        }

        // 정규화된 종료 속도 곡선을 초기화 시 적분한다. 실제 속도를 곱하면 감속 이동 거리가 된다.
        for (int i = 0; i < StopCurveSamples; i++)
        {
            float progress = (i + 0.5f) / StopCurveSamples;
            _walkStopTravelTime += Enemy.Data.CalculateWalkSpeedRatio(EnemyWalkPhase.End, progress);
        }
        _walkStopTravelTime *= _walkEndClip.length / StopCurveSamples;
    }

    /// <summary>PlayerMovement.IsGrounded와 같은 방식 (바닥 방향 접촉 + 실제로 받쳐주는 접촉만)</summary>
    public bool IsGrounded()
    {
        return _groundSensor != null && _groundSensor.IsGrounded();
    }

    public override void KnockbackPhysicsUpdate(bool isHorizontalOnly)
    {
        if (isHorizontalOnly)
        {
            UpdateHorizontalKnockback();
        }
        Rigidbody.linearVelocity = CalculateSafeKnockbackVelocity(Rigidbody.linearVelocity);
    }

    public override Vector2 CalculateSafeKnockbackVelocity(Vector2 velocity)
    {
        velocity.x = _groundSensor != null ? _groundSensor.CalculateSafeKnockbackSpeed(velocity.x) : 0f;
        return velocity;
    }

    public override bool IsKnockbackFinished(bool isHorizontalOnly)
    {
        if (isHorizontalOnly)
        {
            return IsHorizontalKnockbackStopped();
        }

        return IsGrounded() && Rigidbody.linearVelocity.y <= LandingVelocityThreshold;
    }

    public override void PatrolIdleMove()
    {
        if (Enemy.MoveArea == null || !IsGrounded())
        {
            StopMove();
            Enemy.Animation.SetRunning(false);
            return;
        }
        if (_pendingPatrolDirection != 0f)
        {
            if (_walkMotionState == WalkMotionState.Stopping)
            {
                return;
            }
            _patrolDirection = _pendingPatrolDirection;
            _pendingPatrolDirection = 0f;
        }
        float speed = _patrolDirection * Enemy.Data.MoveSpeed;
        if (!CanMoveAhead(speed, CalculateStopLookAhead(speed)))
        {
            RequestStopMove();
            _pendingPatrolDirection = -_patrolDirection;
            return;
        }
        Move(new Vector2(_patrolDirection, 0f));
    }

    public override void MoveToInitialPosition()
    {
        MoveToPosition(Enemy.MoveArea.StartPosition);
    }

    public override bool TryMoveToSearchPosition(Vector2 position)
    {
        float offset = position.x - Rigidbody.position.x;
        float speed = Mathf.Sign(offset) * Enemy.Data.MoveSpeed;
        if (Mathf.Abs(offset) <= PositionTolerance || !IsGrounded() ||
            !CanMoveAhead(speed, CalculateStopLookAhead(speed)))
        {
            return false;
        }
        MoveToPosition(position);
        return true;
    }

    private void MoveToPosition(Vector2 position)
    {
        float directionX = position.x - Rigidbody.position.x;
        if (Mathf.Abs(directionX) <= PositionTolerance)
        {
            StopMove();
            Enemy.Animation.SetRunning(false);
            return;
        }
        float speed = Mathf.Sign(directionX) *
            Mathf.Min(Enemy.Data.MoveSpeed, Mathf.Abs(directionX) / Time.fixedDeltaTime);
        ApplyMoveRequest(speed);
        _walkStepDistance = Mathf.Abs(directionX);
    }

    public void MoveToTargetPosition(Vector2 position)
    {
        MoveToPosition(position);
    }

    public void FaceTarget()
    {
        HandleFlip(Enemy.GetDirectionToPlayer().x);
    }

    public override bool IsArrivedAtStart()
    {
        return Mathf.Abs(Rigidbody.position.x - Enemy.MoveArea.StartPosition.x) <= PositionTolerance && IsGrounded();
    }

    public override void Move(Vector2 direction)
    {
        IsBackMove = false;
        ApplyMoveRequest(direction.x * Enemy.Data.MoveSpeed);
    }

    public override void BackMove(Vector2 direction)
    {
        IsBackMove = true;
        ApplyMoveRequest(-direction.x * Enemy.Data.MoveSpeed);
    }

    private void ApplyMoveRequest(float speed)
    {
        if (_walkMotionState == WalkMotionState.Stopping)
        {
            Enemy.Animation.SetRunning(false);
            return;
        }
        if (Enemy.Data.UseWalkAnimationMotion)
        {
            if (_walkMotionState == WalkMotionState.Moving && _requestedWalkVelocity * speed < 0f)
            {
                RequestStopMove();
                return;
            }
            if (Enemy.Animation.GetWalkPhase(out _) == EnemyWalkPhase.End)
            {
                Enemy.Animation.SetRunning(false);
                return;
            }
        }
        if (!IsGrounded())
        {
            StopMove();
            Enemy.Animation.SetRunning(false);
            return;
        }
        if (speed == 0f || !CanMoveAhead(speed, CalculateStopLookAhead(speed)))
        {
            RequestStopMove();
            return;
        }
        if (Enemy.Data.UseWalkAnimationMotion)
        {
            HandleFlip(speed);
            StartWalk(speed);
            return;
        }
        HandleFlip(speed);
        Enemy.Animation.SetRunning(true);
        Rigidbody.linearVelocity = new Vector2(speed, Rigidbody.linearVelocity.y);
    }

    public override bool IsEndPosition(Vector2 direction)
    {
        float nextX = Rigidbody.position.x - direction.x * Enemy.Data.MoveSpeed * Time.fixedDeltaTime;
        float clampX = Enemy.MoveArea.ClampX(nextX);

        if (Mathf.Abs(nextX - clampX) > 0.0001f)
        {
            return true;
        }

        return false;
    }

    public override void StopMove()
    {
        IsBackMove = false;
        ResetWalkMotion();
        _pendingPatrolDirection = 0f;
        Rigidbody.linearVelocity = new Vector2(0f, Rigidbody.linearVelocity.y);
    }

    public override void RequestStopMove()
    {
        if (!Enemy.Data.UseWalkAnimationMotion)
        {
            base.RequestStopMove();
            Enemy.Animation.SetRunning(false);
            return;
        }
        if (_walkMotionState == WalkMotionState.Moving)
        {
            _walkStopVelocity = Rigidbody.linearVelocity.x;
            _requestedWalkVelocity = 0f;
            _walkMotionState = WalkMotionState.Stopping;
        }
        Enemy.Animation.SetRunning(false);
    }

    public override void UpdateWalkPhysics()
    {
        if (!Enemy.Data.UseWalkAnimationMotion || _walkMotionState == WalkMotionState.Stopped)
        {
            return;
        }

        // 전투·사망 상태의 속도는 해당 상태가 소유한다. 걷기 속도로 덮어쓰지 않는다.
        EnemyState state = Enemy.StateMachine.CurrentState;
        if (!(state is IdleState || state is ChaseState ||
              state is ReturnToStartState || state is GroundRangedSpacingState || state is AlertState))
        {
            ResetWalkMotion();
            return;
        }

        if (!IsGrounded())
        {
            StopMove();
            Enemy.Animation.SetRunning(false);
            return;
        }

        EnemyWalkPhase phase = Enemy.Animation.GetWalkPhase(out float progress);
        float speed = _walkMotionState == WalkMotionState.Stopping
            ? _walkStopVelocity : _requestedWalkVelocity;
        switch (phase)
        {
            case EnemyWalkPhase.Start:
                if (_walkMotionState == WalkMotionState.Moving)
                {
                    speed *= Enemy.Data.CalculateWalkSpeedRatio(phase, progress);
                }
                break;
            case EnemyWalkPhase.Loop:
                break;
            case EnemyWalkPhase.End:
                speed = _walkMotionState == WalkMotionState.Stopping
                    ? _walkStopVelocity * Enemy.Data.CalculateWalkSpeedRatio(phase, progress) : 0f;
                break;
            default:
                speed = 0f;
                if (_walkMotionState == WalkMotionState.Stopping)
                {
                    ResetWalkMotion();
                }
                break;
        }

        if (_walkMotionState == WalkMotionState.Moving)
        {
            float maxSpeed = _walkStepDistance / Time.fixedDeltaTime;
            speed = Mathf.Clamp(speed, -maxSpeed, maxSpeed);
        }
        if (Enemy.MoveArea != null)
        {
            float nextX = Rigidbody.position.x + speed * Time.fixedDeltaTime;
            // 넉백으로 영역 밖에 밀린 몸체를 경계로 순간 이동시키지 않는다.
            if (Mathf.Approximately(Rigidbody.position.x, Enemy.MoveArea.ClampX(Rigidbody.position.x)))
            {
                speed = (Enemy.MoveArea.ClampX(nextX) - Rigidbody.position.x) / Time.fixedDeltaTime;
            }
        }
        // 종료 감속 중에도 검사한다. 넉백 경로에는 적용하지 않는다.
        if (speed != 0f && !CanMoveAhead(speed, Mathf.Abs(speed) * Time.fixedDeltaTime))
        {
            StopMove();
            Enemy.Animation.SetRunning(false);
            return;
        }
        Rigidbody.linearVelocity = new Vector2(speed, Rigidbody.linearVelocity.y);
    }

    private float CalculateStopLookAhead(float speed)
    {
        return Mathf.Abs(speed) * (_walkStopTravelTime + Time.fixedDeltaTime);
    }

    public bool CanMoveInDirection(float direction)
    {
        float speed = direction * Enemy.Data.MoveSpeed;
        return IsGrounded() && CanMoveAhead(speed, CalculateStopLookAhead(speed));
    }

    private bool CanMoveAhead(float speed, float lookAhead)
    {
        return _groundSensor != null && _groundSensor.CanMoveAhead(Enemy.MoveArea, speed, lookAhead);
    }

    private void StartWalk(float velocity)
    {
        _requestedWalkVelocity = velocity;
        _walkStopVelocity = 0f;
        _walkStepDistance = float.PositiveInfinity;
        _walkMotionState = WalkMotionState.Moving;
        Enemy.Animation.SetRunning(true);
    }

    private void ResetWalkMotion()
    {
        _walkMotionState = WalkMotionState.Stopped;
        _requestedWalkVelocity = 0f;
        _walkStopVelocity = 0f;
        _walkStepDistance = float.PositiveInfinity;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        ResetWalkMotion();
        _patrolDirection = 1f;
        _pendingPatrolDirection = 0f;
    }

    private void HandleFlip(float directionX)
    {
        if (Mathf.Abs(directionX) < 0.01f) return;

        Vector3 scale = transform.localScale;
        if (directionX > 0f)
        {
            scale.x = Mathf.Abs(scale.x);
        }
        else if (directionX < 0f)
        {
            scale.x = -Mathf.Abs(scale.x);
        }

        transform.localScale = scale;
    }
}
