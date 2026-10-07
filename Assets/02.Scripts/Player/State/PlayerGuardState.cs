using UnityEngine;

/// <summary>
/// 아래 방향키를 누르고 있는 동안 가드. 누르기 시작한 직후가 패링 판정 (PlayerGuard).
/// 키를 떼거나 가드 게이지가 바닥나면 해제.
/// </summary>
public class PlayerGuardState : PlayerBaseState
{
    public PlayerGuardState(Player player, StateMachine stateMachine)
        : base(player, stateMachine)
    {
    }

    public override void Enter()
    {
        base.Enter();

        _player.SetRunning(false);
        _player.SetGuarding(true);

        // 방금 아래 방향키를 눌러서 들어온 경우만 패링 판정을 엶
        _player.Guard.BeginGuard(_inputHandler.TryUseGuardInput());
        _rigidbody2D.linearVelocity = new Vector2(0f, _rigidbody2D.linearVelocity.y);
    }

    public override void Execute()
    {
        base.Execute();

        if (!_inputHandler.IsGuardKey || _player.Guard.IsGaugeEmpty)
        {
            ExitGuard();
            return;
        }

        if (_inputHandler.TryUseDodgeInput() && _player.Movement.CanDodge)
        {
            _stateMachine.ChangeState(_player.DodgeState);
        }
    }

    public override void PhysicsExecute()
    {
        base.PhysicsExecute();

        _player.Movement.MoveX(0f);

        // 가드 중에는 이동하지 않고 좌우 키로 바라보는 방향만 바꿈
        float inputX = _inputHandler.MoveInputX;
        if (Mathf.Abs(inputX) > 0.01f)
        {
            _player.transform.localScale = new Vector3(Mathf.Sign(inputX), 1f, 1f);
        }
    }

    public override void Exit()
    {
        base.Exit();

        _player.Guard.EndGuard();
        _player.SetGuarding(false);
    }

    private void ExitGuard()
    {
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
