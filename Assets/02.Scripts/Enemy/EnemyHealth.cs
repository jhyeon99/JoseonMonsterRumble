using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable, IKillable
{
    private const int FirstComboStep = 1;
    private const int SecondComboStep = 2;
    [Header("받는 데미지 랜덤 편차")]
    [Tooltip("기본값 2: 타격마다 -2, -1, 0, +1, +2 중 하나를 적용합니다. 0이면 비활성화합니다.")]
    [SerializeField, Min(0)] private int _damageVariance = 2;
    private Enemy _enemy;
    private float _currentHp;
    public event Action OnDeath;

    // 사망하지 않은 피격 시 호출 (피격 연출용)
    public event Action<HitData> OnHit;
    public bool IsAlive => _currentHp > 0f;
    public bool IsDamaged => IsAlive && _currentHp < _enemy.Data.MaxHp;
    public float CurrentHp => _currentHp;
    public bool CanDieInKillZone => _enemy.MoveType is EnemyGroundMove;

    private float _shieldHp;
    public float ShieldHp => _shieldHp;
    public bool HasShield => _shieldHp > 0f;

    public void Heal(float amount)
    {
        if (!IsAlive || _enemy.IsDefeated || amount <= 0f)
        {
            return;
        }

        _currentHp = Mathf.Min(_currentHp + amount, _enemy.Data.MaxHp);
    }

    public void SetShield(float amount)
    {
        _shieldHp = Mathf.Max(0f, amount);
    }

    public void ClearShield()
    {
        _shieldHp = 0f;
    }

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        _currentHp = _enemy.Data.MaxHp;
    }

    public bool TakeDamage(HitData hitData)
    {
        if (_enemy.IsDefeated) return false;

        if (hitData.Damage > 0f && _damageVariance > 0)
        {
            int variance = UnityEngine.Random.Range(-_damageVariance, _damageVariance + 1);
            // 양수 타격은 최소 1을 유지하되, 원래 1 미만인 데미지는 증가시키지 않는다.
            float minimumDamage = Mathf.Min(1f, hitData.Damage);
            hitData.Damage = Mathf.Max(minimumDamage, hitData.Damage + variance);
        }

        if (_shieldHp > 0f)
        {
            if (hitData.IsTalisman || hitData.IsHeavyImpact)
            {
                _shieldHp = 0f;
            }
            else
            {
                float damageToShield = Mathf.Min(_shieldHp, hitData.Damage);
                _shieldHp -= damageToShield;
                hitData.Damage -= damageToShield;
            }
        }

        bool shouldKnockback = hitData.KnockbackForce > 0f && !_enemy.IsGroggy;
        _currentHp -= hitData.Damage;
        GameEvents.TriggerEnemyHit(hitData);

        if (_currentHp <= 0f)
        {
            Die();
            return true;
        }

        _enemy.Animation.PlayDamageHit(hitData);
        OnHit?.Invoke(hitData);

        // 그로기 중에는 넉백으로 그로기가 풀리지 않게 데미지만 받음
        if (shouldKnockback)
        {
            bool isLightComboHit = hitData.ComboStep == FirstComboStep ||
                hitData.ComboStep == SecondComboStep ||
                hitData.IsReflectedProjectile;
            if (!isLightComboHit || !_enemy.MoveType.TryStartLightKnockback(hitData))
            {
                _enemy.StateMachine.ChangeState(new KnockbackState(_enemy, hitData));
            }
        }

        return true;
    }

    public void Kill()
    {
        if (!IsAlive || _enemy.IsDefeated)
        {
            return;
        }

        Die();
    }

    private void Die()
    {
        _currentHp = 0f;
        _enemy.MoveType.ResetLightKnockback();
        OnDeath?.Invoke();
        _enemy.StateMachine.ChangeState(new DeadState(_enemy));
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
