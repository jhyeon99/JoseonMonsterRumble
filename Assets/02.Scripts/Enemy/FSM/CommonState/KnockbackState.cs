using UnityEngine;

public class KnockbackState : EnemyState
{
    private readonly Enemy _enemy;

    public KnockbackState(Enemy enemy)
    {
        _enemy = enemy;
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