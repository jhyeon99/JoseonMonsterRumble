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
        if (_enemy.StateMachine.CurrentState is DeadState) return false;

        if (hitData.KnockbackForce > 0f)
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
            OnHit?.Invoke(hitData);
            _enemy.StateMachine.ChangeState(new KnockbackState(_enemy, hitData));
            return true;
        }

        if (_enemy.StateMachine.CurrentState is AttackPrepareFlyingState ||
            _enemy.StateMachine.CurrentState is AttackState ||
            _enemy.StateMachine.CurrentState is AttackPrepareGroundState)
        {
            return false;
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
        OnHit?.Invoke(hitData);

        return true;
    }
}