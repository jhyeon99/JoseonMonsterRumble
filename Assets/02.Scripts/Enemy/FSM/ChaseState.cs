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
    }

    public override void FixedUpdate()
    {
        _enemy.MoveType.Move(_enemy.GetDirectionToPlayer());
    }

    public override void Update()
    {
        if (_enemy.GetDistanceToPlayer() <= _enemy.Data.AttackDetectRange)
        {
            _enemy.StateMachine.ChangeState(new AttackState(_enemy));
        }

        if (_enemy.GetDistanceToPlayer() >= _enemy.Data.DetectRange)
        {
            _enemy.StateMachine.ChangeState(new IdleState(_enemy));
        }
    }

    public override void Exit()
    {
        Debug.Log("추적 종료");
    }
}