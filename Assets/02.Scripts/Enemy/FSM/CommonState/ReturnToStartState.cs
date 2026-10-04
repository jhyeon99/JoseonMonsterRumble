using UnityEngine;

public class ReturnToStartState : EnemyState
{
    private readonly Enemy _enemy;

    public ReturnToStartState(Enemy enemy)
    {
        _enemy = enemy;
    }

    public override void Enter()
    {
        _enemy.DirectionLock.LockDirection();
        Debug.Log("시작 위치로 복귀 시작");
    }

    public override void FixedUpdate()
    {
        // 시작 위치로 이동
        _enemy.MoveType.MoveToInitialPosition();
    }

    public override void Update()
    {
        // 시작 위치에 거의 도착했으면 Idle 상태로 전환
        if (_enemy.MoveType.IsArrivedAtStart())
        {
            _enemy.StateMachine.ChangeState(new IdleState(_enemy));
        }
    }

    public override void Exit()
    {
        Debug.Log("시작 위치 복귀 완료");
    }
}