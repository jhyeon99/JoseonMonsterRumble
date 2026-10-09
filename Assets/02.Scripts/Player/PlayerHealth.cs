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

    private bool _tutorialProtection;
    private bool _isDead;
    public bool IsDead => _isDead;

    private bool _isInvincible;
    public bool IsInvincible => _isInvincible;

    public HitData LastFatalHitData { get; private set; }

    private const string DefaultKillReason = "처형 / 즉사";

    private SpriteRenderer _spriteRenderer;
    private Coroutine _invincibleRoutine;
    private PlayerGuard _guard;
    private Player _player;

    public event Action<float, float> OnHealthChanged;
    public event Action OnDied;
    public event Action<HitData> OnHit;
    public event Action<int> OnPotionChanged;


    private void Awake()
    {
        _currentHealth = _maxHealth;
        _isDead = false;
        LastFatalHitData = default;
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Start()
    {
        // Player.Awake에서 PlayerGuard를 추가하는 경우가 있어서 Awake가 아닌 Start에서 가져옴
        TryGetComponent(out _guard);
        TryGetComponent(out _player);
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
    }

    /// <summary>연습 중 피해는 막되, 가드와 패링은 실제 판정으로 처리합니다.</summary>
    public void SetTutorialProtection(bool active) => _tutorialProtection = active;

    public bool TakeDamage(HitData hitdata)
    {
        if (IsDead || _isInvincible) return false;

        // 가드 중이면 데미지 대신 가드 / 패링 처리
        if (_guard != null && _guard.TryHandleHit(hitdata) != GuardResult.None) return false;

        if (_tutorialProtection) return false;

        GameEvents.TriggerPlayerHit(hitdata);
        _currentHealth = Mathf.Max(_currentHealth - hitdata.Damage, 0f);

        if (_currentHealth <= 0f)
        {
            if (string.IsNullOrWhiteSpace(hitdata.AttackName))
            {
                hitdata.AttackName = "치명적인 피해";
            }

            LastFatalHitData = hitdata;
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
            Die();
            return true;
        }

        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

        OnHit?.Invoke(hitdata);

        if (TryGetComponent<HitFlash>(out HitFlash flash))
        {
            flash.Flash();
        }

        bool isSuperArmor = _player != null && _player.IsSuperArmorActive();
        if (!isSuperArmor)
        {
            StartInvincible();
        }
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
        Kill(DefaultKillReason);
    }

    public void Kill(string reason)
    {
        if (_isDead) return;

        LastFatalHitData = new HitData(
            damage: _currentHealth,
            knockbackForce: 0f,
            hitPoint: transform.position,
            attackDirection: Vector2.zero,
            defense: default,
            source: null,
            attackName: string.IsNullOrWhiteSpace(reason) ? DefaultKillReason : reason
        );

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

    /// <param name="showVisual">false면 반투명 표시 없이 무적만 적용 (해제할 때는 항상 원래대로)</param>
    public void SetInvincible(bool isInvincible, bool showVisual = true)
    {
        _isInvincible = isInvincible;

        int playerLayer = gameObject.layer;
        int enemyAttackLayer = Constants.Layers.EnemyAttack;

        if (enemyAttackLayer != -1)
        {
            Physics2D.IgnoreLayerCollision(playerLayer, enemyAttackLayer, isInvincible);
        }

        if (_spriteRenderer != null && (showVisual || !isInvincible))
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