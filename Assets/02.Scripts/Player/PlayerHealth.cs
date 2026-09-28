using System;
using System.Collections;
using Unity.Collections;
using UnityEngine;

[RequireComponent(typeof(HitFlash))]
public class PlayerHealth : MonoBehaviour, IDamageable, IKillable
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

        GameEvents.TriggerPlayerHit(hitdata);
        _currentHealth = Mathf.Max(_currentHealth - hitdata.Damage, 0f);
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

        if (_currentHealth <= 0f)
        {
            Die();
            return true;
        }

        if (TryGetComponent<HitFlash>(out HitFlash flash))
        {
            flash.Flash();
        }

        StartInvincible();
        return true;
    }

    public void Kill()
    {
        if (_isDead) return;

        _currentHealth = 0f;
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

        Die();
    }

    private void Die()
    {
        _isDead = true;
        OnDied?.Invoke();
    }

    private void StartInvincible()
    {
        if (_invincibleRoutine != null)
        {
            StopCoroutine(_invincibleRoutine);
        }

        _invincibleRoutine = StartCoroutine(InvincibleRoutine());
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
        int enemyLayer = Constants.Layers.EnemyAttack;

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