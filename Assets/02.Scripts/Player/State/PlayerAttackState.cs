using UnityEngine;

public class PlayerAttackState : PlayerBaseState
{
    private float _attackTimer;
    public PlayerAttackState(Player player, StateMachine stateMachine)
        : base(player, stateMachine) { }

    public override void Enter()
    {
        base.Enter();

        _rigidbody2D.linearVelocity = new Vector2(0f, _rigidbody2D.linearVelocity.y);

        _player.Attack.ExecuteAttack();

        _attackTimer = _player.Attack.CurrentAttackDuration;
    }

    public override void Execute()
    {
        base.Execute();

        if (_player.Health != null && _player.Health.IsDead)
        {
            _stateMachine.ChangeState(_player.DeadState);
            return;
        }

        _attackTimer -= Time.deltaTime;
        if (_attackTimer <= 0f)
        {
            _stateMachine.ChangeState(_player.IdleState);
        }
    }
}
