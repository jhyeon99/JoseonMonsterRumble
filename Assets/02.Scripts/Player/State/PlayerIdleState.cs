using UnityEngine;

public class PlayerIdleState : PlayerBaseState
{
    public PlayerIdleState(Player player, StateMachine stateMachine) 
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
        
        if (Mathf.Abs(_inputHandler.MoveInputX) > 0.01f)
        {
            _stateMachine.ChangeState(_player.MoveState);
            return;
        }
    }

    public override void PhysicsExecute()
    {
        base.PhysicsExecute();

        float targetVelocityX = 0f;

        if (_player.transform.parent != null && 
            _player.transform.parent.TryGetComponent<MovingPlatform>(out var platform))
        {
            targetVelocityX = platform.Velocity.x;
        }

        _rigidbody2D.linearVelocity = new Vector2(targetVelocityX, _rigidbody2D.linearVelocity.y);
    }
}