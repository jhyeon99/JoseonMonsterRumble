using UnityEngine;

public class BuffState : EnemyState
{
    private readonly Enemy _enemy;
    private readonly Transform _targetEnemy;
    private Enemy _target;

    public BuffState(Enemy enemy, Transform target)
    {
        _enemy = enemy;
        _targetEnemy = target;
    }

    public override void Enter()
    {
        Debug.Log("버프 상태 진입!");
        _target = _targetEnemy.GetComponent<Enemy>();
        _target.Combat.IncreaseDamge(_enemy.Data.AttackBuffAmount);
    }

    public override void FixedUpdate()
    {
        _enemy.MoveType.ChaseMove(_targetEnemy);
    }

    public override void Exit()
    {
        _target.Combat.NormalDamage();
    }
}
