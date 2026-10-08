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
        _enemy.Animation.SetRunning(true);
    }

    public override void FixedUpdate()
    {
        if (_enemy.MoveType is EnemyGroundMove groundMove)
        {
            groundMove.PatrolIdleMove();
        }
        else if (_enemy.MoveType is EnemyFlyingMove flyingMove)
        {
            flyingMove.PatrolIdleMove();
        }
    }

    public override void Update()
    {
        if (_enemy.MoveArea != null && _enemy.Target != null)
        {
            if (_enemy.MoveArea.IsPositionInArea(_enemy.Target.position))
            {
                _enemy.StateMachine.ChangeState(new ChaseState(_enemy));
            }
        }
    }

    public override void Exit()
    {
        Debug.Log("Idle 종료");
    }
}
