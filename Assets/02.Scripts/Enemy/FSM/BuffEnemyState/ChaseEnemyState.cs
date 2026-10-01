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
        Vector2 direction = (_targetEnemy.position - _enemy.transform.position).normalized;
        _enemy.MoveType.Move(direction);
    }
}
