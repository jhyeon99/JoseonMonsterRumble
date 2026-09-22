using Unity.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float _maxHealth;
    [SerializeField, ReadOnly] private float _currentHealth;
    public bool IsDead => _currentHealth <= 0;

    private void Awake()
    {
        _currentHealth = _maxHealth;
    }

    public bool TakeDamage(float damage)
    {
        if (IsDead) return false;

        _currentHealth = Mathf.Max(_currentHealth - damage, 0);
        if (_currentHealth <= 0)
        {
            // 사망
        }

        return true;
    }
}