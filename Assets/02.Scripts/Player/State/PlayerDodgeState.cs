using UnityEngine;

public class PlayerDodgeState : PlayerBaseState
{
    private float _elapsedTime;
    private float _dodgeDirection;
    private Vector2 _startPosition;

    public override PlatformInteractionMode PlatformMode => PlatformInteractionMode.ManualWithDelta;

    public PlayerDodgeState(Player player, StateMachine stateMachine)
        : base(player, stateMachine)
    {
    }

    public override void Enter()
    {
        base.Enter();

        _elapsedTime = 0f;
        _player.Health.SetInvincible(true);

        float facingDirection = _player.transform.localScale.x > 0 ? 1f : -1f;
        _dodgeDirection = -facingDirection;

        _startPosition = _rigidbody2D.position;

        _rigidbody2D.linearVelocity = new Vector2(0f, _rigidbody2D.linearVelocity.y);
    }

    public override void Execute()
    {
        base.Execute();

        _elapsedTime += Time.deltaTime;
        if (_elapsedTime >= _player.Movement.DodgeDuration)
        {
            if (Mathf.Abs(_inputHandler.MoveInputX) > 0.01f)
                _stateMachine.ChangeState(_player.MoveState);
            else
                _stateMachine.ChangeState(_player.IdleState);
        }
    }

    public override void PhysicsExecute()
    {
        base.PhysicsExecute();

        float normalizedTime = Mathf.Clamp01(_elapsedTime / _player.Movement.DodgeDuration);
        float progress = _player.Movement.DodgeSpeedCurve.Evaluate(normalizedTime);

        MovingPlatform platform = _player.Movement.CurrentPlatform;
        if (platform != null)
        {
            _startPosition += platform.DeltaPosition;
        }

        float targetX = _startPosition.x + _dodgeDirection * _player.Movement.DodgeDistance * progress;

        _rigidbody2D.MovePosition(new Vector2(targetX, _startPosition.y));
    }

    public override void Exit()
    {
        base.Exit();

        _player.Health.SetInvincible(false);
        _player.Movement.StartDodgeCooldown();
    }
}