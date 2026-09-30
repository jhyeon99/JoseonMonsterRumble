using UnityEngine;

public class PlayerIdleState : PlayerBaseState
{
    public PlayerIdleState(Player player, StateMachine stateMachine)
        : base(player, stateMachine)
    {
    }

    public override void Execute()
    {
        base.Execute();

        if (_player.CanStartClimb(_inputHandler.MoveInputX, _inputHandler.MoveInputY))
        {
            _stateMachine.ChangeState(_player.ClimbState);
            return;
        }

        if (_inputHandler.TryUseAttackInput())
        {
            _stateMachine.ChangeState(_player.AttackState);
            return;
        }

        if (_inputHandler.TryUseDodgeInput() && _player.IsGrounded && _player.Movement.CanDodge)
        {
            _stateMachine.ChangeState(_player.DodgeState);
            return;
        }

        if (_inputHandler.TryUseJumpInput() && _player.Movement.CanJump)
        {
            _stateMachine.ChangeState(_player.JumpState);
            return;
        }

        if (Mathf.Abs(_inputHandler.MoveInputX) > 0.01f)
        {
            _stateMachine.ChangeState(_player.MoveState);
            return;
        }
    }

    public override void PhysicsExecute()
    {
        base.PhysicsExecute();

        _player.Movement.MoveX(0f);
    }
}
