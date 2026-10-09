using UnityEngine;

public class AttackState : EnemyState
{
    private readonly Enemy _enemy;
    private float _attackTimer = 0;
    private float _projectileTimer = 0;
    private bool _hasAttacked = false;

    public AttackState(Enemy enemy)
    {
        _enemy = enemy;
    }

    public override void Enter()
    {
        Debug.Log("공격 시작");
        _enemy.DirectionLock.LockDirection();
        _attackTimer = _enemy.Data.AttackDurationTime + _enemy.Data.AttackDelayTime;
        _projectileTimer = _enemy.Data.AttackDurationTime;
        _hasAttacked = false;
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
    }

    public override void Update()
    {
        _projectileTimer -= Time.deltaTime;
        _attackTimer -= Time.deltaTime;
        if (_enemy.Combat is EnemyRangedCombat)
        {
            if (!_hasAttacked && _projectileTimer <= 0)
            {
                _enemy.Combat.Attack();
                _hasAttacked = true;
            }
        }

        if (_attackTimer <= 0)
        {
            // 타이머만 끝났다고 무기 회수 동작 중에 회복을 시작하지 않는다.
            if (_enemy.Data.HasStationaryAttackCycle && _enemy.Animation.IsPlayingAttack())
            {
                return;
            }
            _enemy.MoveType.StopMove();
            _enemy.StateMachine.ChangeState(new AttackRecoveryState(_enemy));
        }
    }

    public override void Exit()
    {
        Debug.Log("공격 종료");
        if (!_enemy.Data.HasStationaryAttackCycle)
        {
            _enemy.DirectionLock.UnlockDirection();
        }
        if (_enemy.Combat is EnemyMeleeCombat)
        {
            _enemy.MoveType.StopQuadraticCharge();
        }
    }
}
