using UnityEngine;

public class AttackRecoveryState : EnemyState
{
    private readonly Enemy _enemy;
    private float _recoveryTimer = 0;

    public AttackRecoveryState(Enemy enemy)
    {
        _enemy = enemy;
    }

    public override void Enter()
    {
        Debug.Log("휴식 시작");
        _recoveryTimer = _enemy.Data.AttackRecoveryTime;
    }

    public override void FixedUpdate()
    {
        if (_enemy.GetDistanceToPlayer() <= _enemy.Data.AttackDetectRange)
        {
            _enemy.MoveType.BackMove(_enemy.GetDirectionToPlayer());
        }
    }

    public override void Update()
    {
        _recoveryTimer -= Time.deltaTime;
        if (_recoveryTimer <= 0)
        {
            _enemy.StateMachine.ChangeState(new ChaseState(_enemy));
        }
    }

    public override void Exit()
    {
        Debug.Log("휴식 종료");
    }
}
