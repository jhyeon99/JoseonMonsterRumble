using System;
using Unity.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField, ReadOnly] private float _currentHealth;

    public event Action<float, float> OnHealthChanged;

    public float CurrentHealth => _currentHealth;
    public float MaxHealth => _maxHealth;
    public bool IsDead => _currentHealth <= 0;

    private void Awake()
    {
        _currentHealth = _maxHealth;
    }

    private void Start()
    {
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
    }

    public bool TakeDamage(float damage)
    {
        if (IsDead) return false;

        _currentHealth = Mathf.Max(_currentHealth - damage, 0);
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

        if (_currentHealth <= 0)
        {
            // 사망
        }

        return true;
    }

    public bool Heal(float amount)
    {
        if (IsDead || _currentHealth >= _maxHealth) return false;

        _currentHealth = Mathf.Min(_currentHealth + amount, _maxHealth);
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

        return true;
    }
}