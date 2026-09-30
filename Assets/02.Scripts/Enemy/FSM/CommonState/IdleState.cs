using UnityEngine;

public class IdleState : EnemyState
{
    private readonly Enemy _enemy;

    public IdleState(Enemy enemy)
    {
        _enemy = enemy;
    }

    public override void Enter()
    {
        Debug.Log("Idle 시작");
        _enemy.DirectionLock.LockDirection();
        _enemy.MoveType.StopMove();
    }

    public override void Update()
    {
        if (_enemy.GetDistanceToPlayer() <= _enemy.Data.DetectRange)
        {
            _enemy.StateMachine.ChangeState(new ChaseState(_enemy));
        }
    }

    public override void Exit()
    {
        Debug.Log("Idle 종료");
    }
}
