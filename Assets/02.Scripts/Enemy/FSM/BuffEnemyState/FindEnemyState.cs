using UnityEngine;

public class FindEnemyState : EnemyState
{
    private readonly BuffEnemy _enemy;

    public FindEnemyState(BuffEnemy enemy)
    {
        _enemy = enemy;
    }

    public override void Enter()
    {
        _enemy.FindNewTarget();
    }
}
