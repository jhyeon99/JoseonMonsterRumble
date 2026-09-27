using UnityEngine;

public class AttackPrepareFlyingState : EnemyState
{
    private readonly Enemy _enemy;
    private float _attackPrepareTimer = 0;

    public AttackPrepareFlyingState(Enemy enemy)
    {
        _enemy = enemy;
    }

    public override void Enter()
    {
        Debug.Log("공중 이동 적 공격 준비 시작");
        _attackPrepareTimer = _enemy.Data.AttackPrepareTime;
    }

    public override void Update()
    {
        _attackPrepareTimer -= Time.deltaTime;
        if (_attackPrepareTimer <= 0)
        {
            if (_enemy.Combat is EnemyMeleeCombat)
            {
                _enemy.MoveType.ChargeMove(_enemy.GetDirectionToPlayer());
            }

            _enemy.StateMachine.ChangeState(new AttackState(_enemy));
        }
    }

    public override void Exit()
    {
        Debug.Log("공중 이동 적 공격 준비 완료");
    }
}