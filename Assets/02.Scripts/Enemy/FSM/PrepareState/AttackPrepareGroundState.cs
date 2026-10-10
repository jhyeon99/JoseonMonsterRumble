using UnityEngine;

public class AttackPrepareGroundState : EnemyState
{
    private enum PreparationPhase { WaitingForIdle, Preparing, Committed }
    private PreparationPhase _phase;
    protected Enemy _enemy;
    protected float _attackPrepareTimer = 0;
    private ParryWarningFlash _parryWarning;
    private bool _hasWarned;

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

            // 패링 그로기 공격 예고 (적에 ParryWarningFlash가 있을 때만)
            _parryWarning = _enemy.GetComponentInChildren<ParryWarningFlash>();
        }

        _hasWarned = false;
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

        // 공격 판정이 켜지기 Lead Time초 전에 반짝임
        if (_parryWarning != null && !_hasWarned && _attackPrepareTimer <= _parryWarning.LeadTime)
        {
            _parryWarning.Play();
            _hasWarned = true;
        }

        if (_attackPrepareTimer <= 0)
        {
            _phase = PreparationPhase.Committed;
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
    }
}
