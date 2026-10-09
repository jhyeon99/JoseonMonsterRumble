using UnityEngine;

public class EnemyGroundMove : EnemyMoveType
{
    private const float GroundNormalThreshold = 0.7f;
    private const float LandingVelocityThreshold = 0.1f;

    private readonly ContactPoint2D[] _groundContacts = new ContactPoint2D[8];
    private ContactFilter2D _groundFilter;
    private enum WalkMotionState { Stopped, Moving, Stopping }
    private WalkMotionState _walkMotionState;
    private float _requestedWalkVelocity;
    private float _walkStopVelocity;
    private float _walkStepDistance = float.PositiveInfinity;

    protected override void Awake()
    {
        base.Awake();

        _groundFilter.SetLayerMask(Constants.Layers.GroundAllMask);
        _groundFilter.useTriggers = false;
    }

    /// <summary>PlayerMovement.IsGrounded와 같은 방식 (바닥 방향 접촉 + 실제로 받쳐주는 접촉만)</summary>
    public bool IsGrounded()
    {
        int count = Rigidbody.GetContacts(_groundFilter, _groundContacts);
        for (int i = 0; i < count; i++)
        {
            ContactPoint2D contact = _groundContacts[i];

            if (contact.normal.y <= GroundNormalThreshold) continue;
            if (contact.normalImpulse <= 0f) continue;

            return true;
        }

        return false;
    }

    public override void KnockbackPhysicsUpdate(bool isHorizontalOnly)
    {
        if (!isHorizontalOnly)
        {
            return;
        }

        UpdateHorizontalKnockback();
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
        if (Enemy.MoveArea == null) return;

        Vector2 center = Enemy.MoveArea.StartPosition + Enemy.MoveArea.CenterOffset;
        float halfWidth = Enemy.MoveArea.AreaSize.x * 0.5f;
        float minX = center.x - halfWidth;
        float maxX = center.x + halfWidth;
        float width = maxX - minX;

        if (width <= 0f) return;

        float pingPong = Mathf.PingPong(Time.time * Enemy.Data.MoveSpeed, width);
        float targetX = minX + pingPong;

        float directionX = targetX - Rigidbody.position.x;

        HandleFlip(directionX);

        if (Enemy.Data.UseWalkAnimationMotion)
        {
            Move(new Vector2(Mathf.Sign(directionX), 0f));
            _walkStepDistance = Mathf.Abs(directionX);
            return;
        }

        var targetPos = new Vector2(targetX, Rigidbody.position.y);

        var nextPos =
            Vector2.MoveTowards(Rigidbody.position, targetPos, Enemy.Data.MoveSpeed * Time.fixedDeltaTime);
        Rigidbody.MovePosition(nextPos);
    }

    public override void MoveToInitialPosition()
    {
        Vector2 startPos = Enemy.MoveArea.StartPosition;
        float directionX = startPos.x - Rigidbody.position.x;
        HandleFlip(directionX);
        if (Enemy.Data.UseWalkAnimationMotion)
        {
            Move(new Vector2(Mathf.Sign(directionX), 0f));
            _walkStepDistance = Mathf.Abs(directionX);
            return;
        }
        var nextPos = Vector2.MoveTowards(Rigidbody.position, startPos, Enemy.Data.MoveSpeed * Time.fixedDeltaTime);
        Rigidbody.MovePosition(nextPos);
    }

    public override void Move(Vector2 direction)
    {
        IsBackMove = false;
        if (Enemy.Data.UseWalkAnimationMotion)
        {
            StartWalk(direction.x * Enemy.Data.MoveSpeed);
            return;
        }
        Rigidbody.linearVelocity = new Vector2(direction.x * Enemy.Data.MoveSpeed, Rigidbody.linearVelocity.y);
    }

    public override void BackMove(Vector2 direction)
    {
        IsBackMove = true;
        if (Enemy.Data.UseWalkAnimationMotion)
        {
            StartWalk(-direction.x * Enemy.Data.MoveSpeed);
            return;
        }
        Rigidbody.linearVelocity = new Vector2(-direction.x * Enemy.Data.MoveSpeed, Rigidbody.linearVelocity.y);
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
        Rigidbody.linearVelocity = new Vector2(0f, Rigidbody.linearVelocity.y);
    }

    public override void RequestStopMove()
    {
        if (!Enemy.Data.UseWalkAnimationMotion)
        {
            base.RequestStopMove();
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
              state is ReturnToStartState || state is AttackRecoveryState))
        {
            ResetWalkMotion();
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
            speed = (Enemy.MoveArea.ClampX(nextX) - Rigidbody.position.x) / Time.fixedDeltaTime;
        }
        Rigidbody.linearVelocity = new Vector2(speed, Rigidbody.linearVelocity.y);
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
