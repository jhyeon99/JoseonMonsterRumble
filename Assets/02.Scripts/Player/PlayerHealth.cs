using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(HitFlash))]
public class PlayerHealth : MonoBehaviour, IDamageable, IKillable
{
    [SerializeField] private float _maxHealth = 100f;
    public float MaxHealth => _maxHealth;
    [SerializeField, ReadOnly] private float _currentHealth;
    public float CurrentHealth => _currentHealth;
    [SerializeField, ReadOnly] private int _potionCount;
    public int PotionCount => _potionCount;
    [SerializeField] private int _maxPotionCount = 10;
    [SerializeField] private float _potionHealAmount = 10f;

    [SerializeField] private float _invincibleTime = 1.0f;

    private bool _isDead;
    public bool IsDead => _isDead;

    private bool _isInvincible;
    public bool IsInvincible => _isInvincible;

    private SpriteRenderer _spriteRenderer;
    private Coroutine _invincibleRoutine;
    private PlayerGuard _guard;

    public event Action<float, float> OnHealthChanged;
    public event Action OnDied;
    public event Action<HitData> OnHit;
    public event Action<int> OnPotionChanged;


    private void Awake()
    {
        _currentHealth = _maxHealth;
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Start()
    {
        // Player.Awake에서 PlayerGuard를 추가하는 경우가 있어서 Awake가 아닌 Start에서 가져옴
        TryGetComponent(out _guard);
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
    }

    public bool TakeDamage(HitData hitdata)
    {
        if (IsDead || _isInvincible) return false;

        // 가드 중이면 데미지 대신 가드 / 패링 처리
        if (_guard != null && _guard.TryHandleHit(hitdata) != GuardResult.None) return false;

        GameEvents.TriggerPlayerHit(hitdata);
        _currentHealth = Mathf.Max(_currentHealth - hitdata.Damage, 0f);
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

        if (_currentHealth <= 0f)
        {
            Die();
            return true;
        }

        OnHit?.Invoke(hitdata);

        if (TryGetComponent<HitFlash>(out HitFlash flash))
        {
            flash.Flash();
        }

        StartInvincible();
        return true;
    }

    public void Heal(float amount)
    {
        _currentHealth = Mathf.Min(_currentHealth + amount, _maxHealth);
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
    }

    public void AddPotion()
    {
        _potionCount = Mathf.Min(_potionCount + 1, _maxPotionCount);
        OnPotionChanged?.Invoke(_potionCount);
    }

    public bool TryUsePotion()
    {
        if (_isDead || _potionCount <= 0) return false;
        if (Mathf.Approximately(_currentHealth, _maxHealth)) return false;

        Heal(_potionHealAmount);
        _potionCount--;
        OnPotionChanged?.Invoke(_potionCount);
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
        int enemyAttackLayer = Constants.Layers.EnemyAttack;

        if (enemyAttackLayer != -1)
        {
            Physics2D.IgnoreLayerCollision(playerLayer, enemyAttackLayer, isInvincible);
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
        int enemyAttackLayer = Constants.Layers.EnemyAttack;

        if (enemyAttackLayer != -1)
        {
            Physics2D.IgnoreLayerCollision(playerLayer, enemyAttackLayer, false);
        }
    }
}