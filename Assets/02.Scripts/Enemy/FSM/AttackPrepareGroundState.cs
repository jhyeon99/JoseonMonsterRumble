using UnityEngine;

public class AttackPrepareGroundState : AttackPrepareState
{
    public AttackPrepareGroundState(Enemy enemy)
    {
        _enemy = enemy;
    }

    public override void FixedUpdate()
    {
        if (_enemy.Combat is EnemyRangedCombat)
        {
            if (_enemy.GetDistanceToPlayer() <= _enemy.Data.AttackDetectRange)
            {
                _enemy.MoveType.BackMove(_enemy.GetDirectionToPlayer());
            }
        }
    }

    public override void Update()
    {
        _attackPrepareTimer -= Time.deltaTime;
        if (_attackPrepareTimer <= 0)
        {
            Debug.Log("지상 적 공격 준비");
            _enemy.StateMachine.ChangeState(new AttackState(_enemy));
        }
    }
}
