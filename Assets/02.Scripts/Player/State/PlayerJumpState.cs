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
        _player.Movement.SetMovingPlatform(null);
        _player.Movement.Jump();
        _player.SetAirCollider(true);
    }

    public override void Exit()
    {
        base.Exit();

        _player.RestoreGroundColliderDelayed();
    }

    public override void Execute()
    {
        base.Execute();

        if (_player.IsGrounded && _rigidbody2D.linearVelocity.y <= 0.01f)
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

        _player.Movement.MoveX(_inputHandler.MoveInputX);
    }
}