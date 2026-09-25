using System;
using System.Collections;
using Unity.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private float _maxHealth = 100f;
    public float MaxHealth => _maxHealth;
    [SerializeField, ReadOnly] private float _currentHealth;
    public float CurrentHealth => _currentHealth;
    [SerializeField] private float _invincibleTime = 1.0f;

    private bool _isDead;
    public bool IsDead => _isDead;

    private bool _isInvincible;
    public bool IsInvincible => _isInvincible;

    private SpriteRenderer _spriteRenderer;
    private Coroutine _invincibleRoutine;

    public event Action<float, float> OnHealthChanged;
    public event Action OnDied;


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
        if (IsDead || _isInvincible) return false;

        _currentHealth = Mathf.Max(_currentHealth - hitdata.Damage, 0);
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

        StartCoroutine(InvincibleRoutine());

        if (_currentHealth <= 0)
        {
            _isDead = true;
            OnDied?.Invoke();
            return true;
        }

        if (_invincibleRoutine != null)
        {
            StopCoroutine(_invincibleRoutine);
        }
        _invincibleRoutine = StartCoroutine(InvincibleRoutine());

        return true;
    }

    private IEnumerator InvincibleRoutine()
    {
        SetInvincible(true);
        yield return new WaitForSeconds(_invincibleTime);
        SetInvincible(false);
        _invincibleRoutine = null;
    }

    public void SetInvincible(bool isInvincible)
    {
        _isInvincible = isInvincible;

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

    private void OnDisable()
    {
        int playerLayer = gameObject.layer;
        int enemyLayer = Constants.Layers.Enemy;
        if (enemyLayer != -1)
        {
            Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, false);
        }
    }
}