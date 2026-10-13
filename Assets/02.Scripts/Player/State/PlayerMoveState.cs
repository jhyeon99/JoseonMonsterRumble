using UnityEngine;

public class PlayerMoveState : PlayerBaseState
{
    public PlayerMoveState(Player player, StateMachine stateMachine)
        : base(player, stateMachine)
    {
    }

    public override void Enter()
    {
        base.Enter();
        _player.SetRunning(true);
    }

    public override void Execute()
    {
        base.Execute();

        // 아래 방향키: 가드 (가드 중 공격키 = 패링이므로 공격보다 먼저 확인)
        if (_inputHandler.IsGuardKey && _player.IsGrounded && _player.Guard.CanStartGuard)
        {
            _stateMachine.ChangeState(_player.GuardState);
            return;
        }

        if (_inputHandler.TryUsePotionInput() && _player.TryEnterPotion())
        {
            return;
        }

        if (_inputHandler.TryUseAttackInput())
        {
            if (!_player.IsGrounded)
            {
                _player.TryEnterAirAttack();
                return;
            }
            // 앞에 그로기 적이 있으면 일반 공격 대신 마무리 공격
            if (!_player.TryEnterFinisher())
            {
                _stateMachine.ChangeState(_player.AttackState);
            }

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
