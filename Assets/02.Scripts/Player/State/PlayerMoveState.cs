using UnityEngine;

public class PlayerMoveState : PlayerBaseState
{
    public PlayerMoveState(Player player, StateMachine stateMachine) 
        : base(player, stateMachine) { }

    public override void Execute()
    {
        base.Execute();
        
        if (_player.Health != null && _player.Health.IsDead)
        {
            _stateMachine.ChangeState(_player.DeadState);
            return;
        }

        if (_inputHandler.IsAttackKeyDown)
        {
            _stateMachine.ChangeState(_player.AttackState);
            return;
        }

        if (_inputHandler.IsDodgeKeyDown && _player.IsGrounded)
        {
            _stateMachine.ChangeState(_player.DodgeState);
            return;
        }

        if (_inputHandler.IsJumpKeyDown && _player.IsGrounded)
        {
            _stateMachine.ChangeState(_player.JumpState);
            return;
        }

        if (Mathf.Abs(_inputHandler.MoveInputX) <= 0.01f)
        {
            _stateMachine.ChangeState(_player.IdleState);
            return;
        }
    }

    public override void PhysicsExecute()
    {
        base.PhysicsExecute();
        
        _player.Movement.MoveX(_inputHandler.MoveInputX);
    }
}