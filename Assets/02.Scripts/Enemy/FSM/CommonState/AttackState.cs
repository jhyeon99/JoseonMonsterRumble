using UnityEngine;

public class AttackState : EnemyState
{
    private readonly Enemy _enemy;
    private float _attackTimer = 0;

    public AttackState(Enemy enemy)
    {
        _enemy = enemy;
    }

    public override void Enter()
    {
        Debug.Log("공격 시작");
        _enemy.DirectionLock.LockDirection();
        _attackTimer = _enemy.Data.AttackDurationTime + _enemy.Data.AttackRecoveryTime;
        _enemy.Combat.Attack();
    }

    public override void Update()
    {
        _attackTimer -= Time.deltaTime;
        if (_attackTimer <= 0)
        {
            _enemy.MoveType.StopMove();
            _enemy.StateMachine.ChangeState(new AttackRecoveryState(_enemy));
        }
    }

    public override void Exit()
    {
        Debug.Log("공격 종료");
        _enemy.DirectionLock.UnlockDirection();
    }
}