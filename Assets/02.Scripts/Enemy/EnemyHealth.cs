using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    private Enemy _enemy;
    private float _currentHp;
    public event Action OnDeath;

    // 사망하지 않은 피격 시 호출 (피격 연출용)
    public event Action<HitData> OnHit;

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        _currentHp = _enemy.Data.MaxHp;
    }

    public bool TakeDamage(HitData hitData)
    {
        if (_enemy.IsDefeated) return false;

        // 그로기 중에는 넉백으로 그로기가 풀리지 않게 데미지만 받음
        if (hitData.KnockbackForce > 0f && !_enemy.IsGroggy)
        {
            _currentHp -= hitData.Damage;
            Debug.Log($"현재 체력 {_currentHp}");

            if (_currentHp <= 0f)
            {
                _currentHp = 0f;
                OnDeath?.Invoke();
                _enemy.StateMachine.ChangeState(new DeadState(_enemy));
                return true;
            }

            _enemy.HitFlash.Flash();
            _enemy.Animation.PlayHit();
            OnHit?.Invoke(hitData);
            _enemy.StateMachine.ChangeState(new KnockbackState(_enemy, hitData));
            return true;
        }

        _currentHp -= hitData.Damage;

        if (_currentHp <= 0f)
        {
            _currentHp = 0f;
            OnDeath?.Invoke();
            _enemy.StateMachine.ChangeState(new DeadState(_enemy));
            return true;
        }

        Debug.Log($"현재 체력 {_currentHp}");
        _enemy.HitFlash.Flash();
        _enemy.Animation.PlayHit();
        OnHit?.Invoke(hitData);

        return true;
    }

    /// <summary>마무리 공격으로 날려 보냄. 체력과 관계없이 사망 처리 (OnDeath 발생)</summary>
    public void Launch(LaunchSettings settings, float directionX)
    {
        if (_enemy.IsDefeated) return;

        _currentHp = 0f;
        OnDeath?.Invoke();
        _enemy.StateMachine.ChangeState(new LaunchedState(_enemy, settings, directionX));
    }
}