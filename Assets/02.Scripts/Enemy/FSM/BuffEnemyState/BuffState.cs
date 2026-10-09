using UnityEngine;

public class BuffState : EnemyState
{
    private readonly Enemy _enemy;
    private readonly Enemy _targetEnemy;
    private readonly BuffEnemy _buffEnemy;
    private readonly SO_BuffTypeData _buffData;

    public BuffState(Enemy enemy, Enemy target)
    {
        _enemy = enemy;
        _targetEnemy = target;
        _buffEnemy = enemy as BuffEnemy;
        _buffData = _buffEnemy != null ? _buffEnemy.CurrentBuffData : null;
    }

    public override void Enter()
    {
        if (_targetEnemy == null)
        {
            return;
        }

        ApplyBuffEffects();

        if (_buffEnemy != null && _buffEnemy.VisualLink != null)
        {
            _buffEnemy.VisualLink.StartLink(_targetEnemy, _buffData);
        }
    }

    private void ApplyBuffEffects()
    {
        if (_buffData == null)
        {
            _targetEnemy.Combat.IncreaseDamage(_enemy.Data.AttackBuffAmount);
            return;
        }

        switch (_buffData.BuffType)
        {
            case DokkebibulBuffType.Red_Attack:
                float attackIncrease = _targetEnemy.Data.MeleeAttackDamage * (_buffData.AttackDamageMultiplier - 1f);
                _targetEnemy.Combat.IncreaseDamage(Mathf.Max(attackIncrease, _enemy.Data.AttackBuffAmount));
                break;

            case DokkebibulBuffType.Green_Heal:
                // 녹색은 Update에서 초당 지속 힐 수행
                break;

            case DokkebibulBuffType.Blue_GuardBreak:
                _targetEnemy.Combat.SetExtraGuardGaugeCost(_buffData.ExtraGuardGaugeCost);
                _targetEnemy.Combat.IncreaseDamage(_targetEnemy.Data.MeleeAttackDamage * 0.2f);
                break;

            case DokkebibulBuffType.Yellow_Shield:
                float shieldAmount = _targetEnemy.Data.MaxHp * _buffData.ShieldRatio;
                _targetEnemy.Health.SetShield(shieldAmount);
                break;
        }
    }

    public override void Update()
    {
        if (_targetEnemy == null || !_targetEnemy.Health.IsAlive)
        {
            return;
        }

        if (_buffData != null && _buffData.BuffType == DokkebibulBuffType.Green_Heal)
        {
            float healAmount = _targetEnemy.Data.MaxHp * _buffData.HealPercentPerSecond * Time.deltaTime;
            _targetEnemy.Health.Heal(healAmount);
        }
    }

    public override void FixedUpdate()
    {
        if (_targetEnemy != null)
        {
            _enemy.MoveType.ChaseMove(_targetEnemy);
        }
    }

    public override void Exit()
    {
        if (_targetEnemy != null)
        {
            _targetEnemy.Combat.NormalDamage();
            _targetEnemy.Combat.ResetExtraGuardGaugeCost();
            _targetEnemy.Health.ClearShield();
        }

        if (_buffEnemy != null && _buffEnemy.VisualLink != null)
        {
            _buffEnemy.VisualLink.StopLink();
        }
    }
}
