using UnityEngine;

public class EnragedState : EnemyState
{
    private Enemy _enemy;

    public EnragedState(Enemy enemy)
    {
        _enemy = enemy;
    }

    public override void Enter()
    {
        Debug.Log("도깨비불 광폭화!!");
    }

    public override void FixedUpdate()
    {
        _enemy.MoveType.Move(_enemy.GetDirectionToPlayer());
        if (_enemy.GetDistanceToPlayer() <= _enemy.Data.AttackDetectRange)
        {
            _enemy.Combat.Attack();
            _enemy.MoveType.StopMove();
        }
    }
}
