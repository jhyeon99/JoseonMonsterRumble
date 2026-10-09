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
        _enemy.Animation.SetRunning(true);
        if (_enemy.Combat is EnemyMeleeCombat melee && melee.FlyingAttack != null)
        {
            melee.FlyingAttack.PlayFlightAnimation();
        }
    }

    public override void FixedUpdate()
    {
        _enemy.MoveType.MoveToInitialPosition();
    }

    public override void Update()
    {
        if (_enemy.MoveArea != null &&
            _enemy.Target != null && _enemy.MoveArea.IsPositionDetected(_enemy.Target.position))
        {
            _enemy.StateMachine.ChangeState(new ChaseState(_enemy));
            return;
        }

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
