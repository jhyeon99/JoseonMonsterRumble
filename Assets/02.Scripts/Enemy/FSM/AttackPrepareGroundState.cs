using UnityEngine;

public class AttackPrepareGroundState : EnemyState
{
    private readonly Enemy _enemy;
    private float _attackTimer = 0;

    public AttackPrepareGroundState(Enemy enemy)
    {
        _enemy = enemy;
    }

    public override void Enter()
    {
        Debug.Log("지상 이동 적 공격 준비 시작");
    }

    public override void Update()
    {
    }

    public override void Exit()
    {
        Debug.Log("지상 이동 적 공격 준비 완료");
    }
}