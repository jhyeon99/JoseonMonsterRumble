using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    private Enemy _enemy;
    private float _currentHp;
    public event Action OnDeath;

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
            _enemy.StateMachine.ChangeState(new KnockbackState(_enemy, hitData));
            return true;
        }

        if (_enemy.StateMachine.CurrentState is AttackPrepareState ||
            _enemy.StateMachine.CurrentState is AttackState)
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

        return true;
    }
}
