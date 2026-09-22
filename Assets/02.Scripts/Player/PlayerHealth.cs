using System;
using System.Collections;
using Unity.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField, ReadOnly] private float _currentHealth;
    [SerializeField] private float _invincibleTime = 1.0f;

    private bool _isDead;
    private SpriteRenderer _spriteRenderer;

    public event Action<float, float> OnHealthChanged;

    public float CurrentHealth => _currentHealth;
    public float MaxHealth => _maxHealth;
    public bool IsDead => _isDead;

    private void Awake()
    {
        _currentHealth = _maxHealth;
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
    }

    public bool TakeDamage(HitData hitdata)
    {
        if (IsDead) return false;

        _currentHealth = Mathf.Max(_currentHealth - hitdata.Damage, 0);
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

        StartCoroutine(InvincibleRoutine());

        if (_currentHealth <= 0)
        {
            _isDead = true;
        }

        return true;
    }

    public bool TakeDamage(float damage)
    {
        if (IsDead) return false;

        _currentHealth = Mathf.Max(_currentHealth - damage, 0);
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

        StartCoroutine(InvincibleRoutine());

        if (_currentHealth <= 0)
        {
            _isDead = true;
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

    private IEnumerator InvincibleRoutine()
    {
        SetInvincible(true);
        yield return new WaitForSeconds(_invincibleTime);
        SetInvincible(false);
    }

    public void SetInvincible(bool isInvincible)
    {
        int playerLayer = gameObject.layer;
        int enemyLayer = Constants.Layers.Enemy;

        if (enemyLayer != -1)
        {
            Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, isInvincible);
        }

        if (_spriteRenderer != null)
        {
            Color color = _spriteRenderer.color;
            color.a = isInvincible ? 0.5f : 1.0f;
            _spriteRenderer.color = color;
        }
    }
}