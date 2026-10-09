using UnityEngine;

public class AttackPrepareGroundState : EnemyState
{
    private enum PreparationPhase { WaitingForIdle, Preparing, Committed }
    private PreparationPhase _phase;
    protected Enemy _enemy;
    protected float _attackPrepareTimer = 0;

    public AttackPrepareGroundState(Enemy enemy)
    {
        _enemy = enemy;
    }

    public static EnemyState CreateAttackEntryState(Enemy enemy)
    {
        if (enemy.Combat is EnemyRangedCombat &&
            (enemy.Data.RangedAttackIdleDuration <= 0f || Random.value >= enemy.Data.RangedAttackIdleChance))
        {
            return new AttackState(enemy);
        }
        return new AttackPrepareGroundState(enemy);
    }

    public override void Enter()
    {
        Debug.Log("적 공격 준비 시작");
        bool isRanged = _enemy.Combat is EnemyRangedCombat;
        _phase = isRanged ? PreparationPhase.WaitingForIdle : PreparationPhase.Preparing;
        _attackPrepareTimer = isRanged ? _enemy.Data.RangedAttackIdleDuration : _enemy.Data.AttackPrepareTime;
        _enemy.MoveType.StopMove();
        _enemy.Animation.SetRunning(false);
        ((EnemyGroundMove)_enemy.MoveType).FaceTarget();
        _enemy.DirectionLock.LockDirection();
        if (_enemy.Combat is EnemyMeleeCombat)
        {
            _enemy.Animation.PlayAttack();
        }
    }

    public override void Update()
    {
        if (_phase == PreparationPhase.WaitingForIdle)
        {
            if (_enemy.Animation.GetWalkPhase(out _) == EnemyWalkPhase.None &&
                !_enemy.Animation.IsPlayingAttack() && !_enemy.Animation.IsPlayingHit())
            {
                _phase = PreparationPhase.Preparing;
            }
            return;
        }
        _attackPrepareTimer -= Time.deltaTime;
        if (_attackPrepareTimer <= 0)
        {
            _phase = PreparationPhase.Committed;
            Debug.Log("지상 적 공격 준비");
            _enemy.StateMachine.ChangeState(new AttackState(_enemy));
        }
    }

    public override void Exit()
    {
        _enemy.MoveType.IsBackMove = false;
        _enemy.MoveType.StopMove();
        if (_phase != PreparationPhase.Committed)
        {
            _enemy.Animation.ResetAttackTrigger();
        }
        _attackPrepareTimer = 0f;
        _phase = PreparationPhase.Preparing;
        Debug.Log("적 공격 준비 완료");
    }
}
