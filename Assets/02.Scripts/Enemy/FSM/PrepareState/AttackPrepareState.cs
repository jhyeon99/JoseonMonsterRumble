using UnityEngine;

public class AttackPrepareState : EnemyState
{
    protected Enemy _enemy;
    protected float _attackPrepareTimer = 0;

    public override void Enter()
    {
        Debug.Log("적 공격 준비 시작");
        _attackPrepareTimer = _enemy.Data.AttackPrepareTime;
        _enemy.MoveType.StopMove();
    }

    public override void Exit()
    {
        Debug.Log("적 공격 준비 완료");
    }
}
