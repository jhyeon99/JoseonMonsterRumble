using UnityEngine;

public class AttackPrepareGroundState : AttackPrepareState
{
    public AttackPrepareGroundState(Enemy enemy)
    {
        _enemy = enemy;
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