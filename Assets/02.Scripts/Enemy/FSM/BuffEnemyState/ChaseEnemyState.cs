using UnityEngine;

public class ChaseEnemyState : EnemyState
{
    private readonly Enemy _enemy;
    private readonly Transform _targetEnemy;

    public ChaseEnemyState(Enemy enemy, Transform target)
    {
        _enemy = enemy;
        _targetEnemy = target;
    }

    public override void FixedUpdate()
    {
        _enemy.MoveType.MoveToStartPosition(_targetEnemy);
    }

    public override void Exit()
    {
        _enemy.MoveType.StopMove();
    }
}
