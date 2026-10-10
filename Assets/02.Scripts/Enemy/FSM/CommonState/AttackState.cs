using UnityEngine;

public class AttackState : EnemyState
{
    private enum AnimationPlayback { AwaitingEntry, Playing, Completed }
    private readonly Enemy _enemy;
    private float _attackTimer = 0;
    private float _projectileTimer = 0;
    private bool _hasAttacked = false;
    private const float SoftLockLeadTime = 0.2f;
    private bool _isSoftLocked = false;
    private AnimationPlayback _animationPlayback;

    public AttackState(Enemy enemy)
    {
        _enemy = enemy;
    }

    public override void Enter()
    {
        _enemy.MoveType.StopMove();
        _enemy.Animation.SetRunning(false);
        if (_enemy.MoveType is EnemyGroundMove groundMove && _enemy.Combat is EnemyRangedCombat)
        {
            groundMove.FaceTarget();
        }

        if (_enemy.Combat is EnemyRangedCombat)
        {
            _isSoftLocked = false;
            _enemy.DirectionLock.UnlockDirection();
        }
        else
        {
            _isSoftLocked = true;
            _enemy.DirectionLock.LockDirection();
        }

        _attackTimer = _enemy.Data.AttackDurationTime + _enemy.Data.AttackDelayTime;
        _projectileTimer = _enemy.Data.AttackDurationTime;
        _hasAttacked = false;
        _animationPlayback = _enemy.Animation.IsPlayingAttack()
            ? AnimationPlayback.Playing : AnimationPlayback.AwaitingEntry;
        if (_enemy.Combat is EnemyMeleeCombat)
        {
            _enemy.Combat.Attack();
        }
        else
        {
            _enemy.Animation.PlayAttack();
        }
    }

    public override void FixedUpdate()
    {
        if (_enemy.Combat is EnemyMeleeCombat)
        {
            _enemy.MoveType.QuadraticChargePhysicsUpdate();
        }
        else
        {
            _enemy.MoveType.StopMove();
        }
    }

    public override void Update()
    {
        if (_enemy.MoveType is EnemyGroundMove)
        {
            bool isPlaying = _enemy.Animation.IsPlayingAttack();
            if (_animationPlayback == AnimationPlayback.AwaitingEntry && isPlaying)
            {
                _animationPlayback = AnimationPlayback.Playing;
            }
            else if (_animationPlayback == AnimationPlayback.Playing && !isPlaying)
            {
                _animationPlayback = AnimationPlayback.Completed;
            }
        }
        _projectileTimer -= Time.deltaTime;
        _attackTimer -= Time.deltaTime;
        if (_enemy.Combat is EnemyRangedCombat rangedCombat)
        {
            if (!_isSoftLocked && _projectileTimer <= SoftLockLeadTime)
            {
                _isSoftLocked = true;
                _enemy.DirectionLock.LockDirection();
                rangedCombat.OnTelegraph();
            }

            if (!_hasAttacked && _projectileTimer <= 0)
            {
                rangedCombat.Attack();
                _hasAttacked = true;
            }
        }

        if (_attackTimer <= 0)
        {
            // 타이머만 끝났다고 무기 회수 동작 중에 회복을 시작하지 않는다.
            if ((_enemy.Combat is EnemyRangedCombat rangedAttack && rangedAttack.IsBurstPending) ||
                (_enemy.MoveType is EnemyGroundMove && _animationPlayback == AnimationPlayback.Playing) ||
                (!(_enemy.MoveType is EnemyGroundMove) &&
                 (_enemy.Data.HasStationaryAttackCycle || _enemy.Combat is EnemyRangedCombat) && _enemy.Animation.IsPlayingAttack()))
            {
                return;
            }
            if (_enemy.MoveType is EnemyGroundMove && _animationPlayback == AnimationPlayback.AwaitingEntry)
            {
                Debug.LogError("Enemy: 공격 재생 진입을 확인하지 못했습니다. Animator의 Attack 태그와 전이를 확인하세요.", _enemy);
            }
            _enemy.MoveType.StopMove();
            _enemy.StateMachine.ChangeState(new AttackRecoveryState(_enemy));
        }
    }

    public override void Exit()
    {
        if (_enemy.Combat is EnemyRangedCombat || (!(_enemy.MoveType is EnemyGroundMove) && !_enemy.Data.HasStationaryAttackCycle))
        {
            _enemy.DirectionLock.UnlockDirection();
        }
        _isSoftLocked = false;
        _enemy.Combat.StopAttack();
        if (_enemy.Combat is EnemyMeleeCombat)
        {
            _enemy.MoveType.StopQuadraticCharge();
        }
        _enemy.Animation.ResetAttackTrigger();
        _attackTimer = 0f;
        _projectileTimer = 0f;
        _hasAttacked = false;
        _animationPlayback = AnimationPlayback.AwaitingEntry;
    }
}
