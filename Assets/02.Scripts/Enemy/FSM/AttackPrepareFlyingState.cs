using UnityEngine;

public class AttackPrepareFlyingState : AttackPrepareState
{
    public AttackPrepareFlyingState(Enemy enemy)
    {
        _enemy = enemy;
    }

    public override void Update()
    {
        _attackPrepareTimer -= Time.deltaTime;
        if (_attackPrepareTimer <= 0)
        {
            Debug.Log("공중 적 공격 준비");
            if (_enemy.Combat is EnemyMeleeCombat)
            {
                _enemy.MoveType.ChargeMove(_enemy.GetDirectionToPlayer());
            }

            _enemy.StateMachine.ChangeState(new AttackState(_enemy));
        }
    }
}
