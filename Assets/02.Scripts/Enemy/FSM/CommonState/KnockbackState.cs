using UnityEngine;

public class KnockbackState : EnemyState
{
    private readonly Enemy _enemy;
    private HitData _hitData;

    public KnockbackState(Enemy enemy, HitData hitData)
    {
        _enemy = enemy;
        _hitData = hitData;
    }

    public override void Enter()
    {
        Debug.Log("적 넉백!");
    }

    public override void Update()
    {
    }

    public override void Exit()
    {
        Debug.Log("적 넉백 종료!");
    }
}