using UnityEngine;

public class ChaseEnemyState : EnemyState
{
    private readonly BuffEnemy _enemy;
    private readonly Transform _target;

    public ChaseEnemyState(BuffEnemy enemy, Transform target)
    {
        _enemy = enemy;
        _target = target;
    }

    public override void FixedUpdate()
    {
        _enemy.BuffMove.Move(_target);
    }
}
