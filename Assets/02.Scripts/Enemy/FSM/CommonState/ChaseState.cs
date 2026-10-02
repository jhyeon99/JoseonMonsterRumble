using UnityEngine;

public class ChaseState : EnemyState
{
    private readonly Enemy _enemy;

    public ChaseState(Enemy enemy)
    {
        _enemy = enemy;
    }

    public override void Enter()
    {
        Debug.Log("추적 시작");
        _enemy.DirectionLock.UnlockDirection();
    }

    public override void FixedUpdate()
    {
        _enemy.MoveType.Move(_enemy.GetDirectionToPlayer());
    }

    public override void Update()
    {
        if (_enemy.MoveArea != null && _enemy.Target != null)
        {
            if (!_enemy.MoveArea.IsPositionInArea(_enemy.Target.position))
            {
                _enemy.StateMachine.ChangeState(new ReturnToStartState(_enemy));
                return;
            }
        }

        if (_enemy.GetDistanceToPlayer() <= _enemy.Data.AttackDetectRange)
        {
            if (_enemy.MoveType is EnemyGroundMove)
            {
                _enemy.StateMachine.ChangeState(new AttackPrepareGroundState(_enemy));
            }
            else if (_enemy.MoveType is EnemyFlyingMove)
            {
                _enemy.StateMachine.ChangeState(new AttackPrepareFlyingState(_enemy));
            }
        }
    }

    public override void Exit()
    {
        Debug.Log("추적 종료");
    }
}
