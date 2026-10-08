using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    private const int FirstComboStep = 1;
    private const int SecondComboStep = 2;
    private Enemy _enemy;
    private float _currentHp;
    public event Action OnDeath;

    // 사망하지 않은 피격 시 호출 (피격 연출용)
    public event Action<HitData> OnHit;
    public bool IsAlive => _currentHp > 0f;
    public bool IsDamaged => IsAlive && _currentHp < _enemy.Data.MaxHp;
    public float CurrentHp => _currentHp;

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        _currentHp = _enemy.Data.MaxHp;
    }

    public bool TakeDamage(HitData hitData)
    {
        if (_enemy.IsDefeated) return false;

        bool shouldKnockback = hitData.KnockbackForce > 0f && !_enemy.IsGroggy;
        _currentHp -= hitData.Damage;
        Debug.Log($"현재 체력 {_currentHp}");
        GameEvents.TriggerEnemyHit(hitData);

        if (_currentHp <= 0f)
        {
            _currentHp = 0f;
            _enemy.MoveType.ResetLightKnockback();
            OnDeath?.Invoke();
            _enemy.StateMachine.ChangeState(new DeadState(_enemy));
            return true;
        }

        _enemy.HitFlash.Flash();
        _enemy.Animation.PlayDamageHit(hitData);
        OnHit?.Invoke(hitData);

        // 그로기 중에는 넉백으로 그로기가 풀리지 않게 데미지만 받음
        if (shouldKnockback)
        {
            bool isLightComboHit = hitData.ComboStep == FirstComboStep || hitData.ComboStep == SecondComboStep;
            if (!isLightComboHit || !_enemy.MoveType.TryStartLightKnockback(hitData))
            {
                _enemy.StateMachine.ChangeState(new KnockbackState(_enemy, hitData));
            }
        }

        return true;
    }

    /// <summary>마무리 공격으로 날려 보냄. 체력과 관계없이 사망 처리 (OnDeath 발생)</summary>
    public void Launch(LaunchSettings settings, float directionX)
    {
        if (_enemy.IsDefeated) return;

        _currentHp = 0f;
        _enemy.MoveType.ResetLightKnockback();
        OnDeath?.Invoke();
        _enemy.StateMachine.ChangeState(new LaunchedState(_enemy, settings, directionX));
    }
}
