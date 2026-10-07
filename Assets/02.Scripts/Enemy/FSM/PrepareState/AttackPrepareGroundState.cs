using UnityEngine;

public class AttackPrepareGroundState : EnemyState
{
    protected Enemy _enemy;
    protected float _attackPrepareTimer = 0;

    public AttackPrepareGroundState(Enemy enemy)
    {
        _enemy = enemy;
    }

    public override void Enter()
    {
        Debug.Log("적 공격 준비 시작");
        _attackPrepareTimer = _enemy.Data.AttackPrepareTime;
        _enemy.MoveType.StopMove();
        if (_enemy.Combat is EnemyMeleeCombat)
        {
            _enemy.Animation.PlayAttack();
            _enemy.DirectionLock.LockDirection();
        }
    }

    public override void FixedUpdate()
    {
        if (_enemy.Combat is EnemyRangedCombat)
        {
            if (_enemy.GetDistanceToPlayer() <= _enemy.Data.AttackDetectRange)
            {
                _enemy.Animation.SetRunning(true);
                _enemy.MoveType.BackMove(_enemy.GetDirectionToPlayer());
            }
        }
    }

    public override void Update()
    {
        _attackPrepareTimer -= Time.deltaTime;
        if (_attackPrepareTimer <= 0)
        {
            Debug.Log("지상 적 공격 준비");
            _enemy.StateMachine.ChangeState(new AttackState(_enemy));
        }
    }

    public override void Exit()
    {
        _enemy.MoveType.IsBackMove = false;
        _enemy.MoveType.StopMove();
        Debug.Log("적 공격 준비 완료");
    }
}
