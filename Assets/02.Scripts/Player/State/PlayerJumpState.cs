using UnityEngine;

public class PlayerJumpState : PlayerBaseState
{
    public PlayerJumpState(Player player, StateMachine stateMachine)
        : base(player, stateMachine)
    {
    }

    public override PlatformInteractionMode PlatformMode => PlatformInteractionMode.FreeTrajectory;

    public override void Enter()
    {
        base.Enter();

        _rigidbody2D.linearVelocity = new Vector2(_rigidbody2D.linearVelocity.x, 0);
        _player.Movement.Jump();
        _player.Movement.SetMovingPlatform(null);
    }

    public override void Exit()
    {
        base.Exit();

        if (_player.IsGrounded)
        {
            _player.Movement.OnLanded();
        }
    }

    public override void Execute()
    {
        base.Execute();

        float platformVelocityY =
            _player.Movement.CurrentPlatform != null ? _player.Movement.CurrentPlatform.Velocity.y : 0f;

        float relativeVelocityY = _rigidbody2D.linearVelocity.y - platformVelocityY;

        if (!_player.IsGrounded && _inputHandler.TryUseAttackInput() && _player.TryEnterAirAttack())
        {
            return;
        }

        if (_player.IsGrounded && relativeVelocityY <= 0.1f)
        {
            _player.Movement.OnLanded();

            if (Mathf.Abs(_inputHandler.MoveInputX) > 0.01f)
            {
                _stateMachine.ChangeState(_player.MoveState);
            }
            else
            {
                _stateMachine.ChangeState(_player.IdleState);
            }
        }
    }

    public override void PhysicsExecute()
    {
        base.PhysicsExecute();

        _player.Movement.MoveX(_inputHandler.MoveInputX);
    }
}
