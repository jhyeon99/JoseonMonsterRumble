using UnityEngine;

public class BuffState : EnemyState
{
    private readonly Enemy _enemy;
    private readonly Enemy _targetEnemy;

    public BuffState(Enemy enemy, Enemy target)
    {
        _enemy = enemy;
        _targetEnemy = target;
    }

    public override void Enter()
    {
        Debug.Log("버프 상태 진입!");
        _targetEnemy.Combat.IncreaseDamage(_enemy.Data.AttackBuffAmount);
    }

    public override void FixedUpdate()
    {
        _enemy.MoveType.ChaseMove(_targetEnemy);
    }

    public override void Exit()
    {
        _targetEnemy.Combat.NormalDamage();
    }
}
