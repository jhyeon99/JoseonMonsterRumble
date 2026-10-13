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
    [SerializeField] private int _maxPotionCount = 3;
    [SerializeField, Range(0.1f, 1.0f)] private float _potionHealRatio = 0.7f;
    [SerializeField, Min(0.1f)] private float _potionCooldown = 0.48f;

    private float _potionCooldownTimer;
    public float PotionCooldown => _potionCooldown;
    public float PotionCooldownTimer => _potionCooldownTimer;
    public float PotionCooldownRatio => _potionCooldown > 0f ? Mathf.Clamp01(_potionCooldownTimer / _potionCooldown) : 0f;
    public bool CanUsePotion => !_isDead && _potionCount > 0 && _potionCooldownTimer <= 0f && !Mathf.Approximately(_currentHealth, _maxHealth);

    [SerializeField] private float _invincibleTime = 1.0f;

    [Header("독 (상태이상)")]
    [Tooltip("독 한 번에 깎이는 체력")]
    [SerializeField, Min(0f)] private float _poisonTickDamage = 1f;

    [Tooltip("독 피해 간격 (초)")]
    [SerializeField, Min(0.05f)] private float _poisonTickInterval = 1f;

    [Tooltip("끄면 독으로는 체력 1 아래로 내려가지 않음 (독으로 죽지 않음)")]
    [SerializeField] private bool _poisonCanKill = true;

    [Tooltip("사망 원인 표시 이름")]
    [SerializeField] private string _poisonKillReason = "독";

    private float _poisonRemaining;
    private float _poisonTickTimer;

    /// <summary>지금 독에 걸려 있는지</summary>
    public bool IsPoisoned => _poisonRemaining > 0f && !_isDead;

    /// <summary>남은 독 시간 (초)</summary>
    public float PoisonRemaining => IsPoisoned ? _poisonRemaining : 0f;

    /// <summary>독이 걸리거나(true) 풀릴 때(false)</summary>
    public event Action<bool> OnPoisonChanged;

    /// <summary>독으로 체력이 깎일 때 (실제로 깎인 양). 데미지 숫자 표시용</summary>
    public event Action<float> OnPoisonDamaged;

    private bool _isDead;
    public bool IsDead => _isDead;

    private bool _isInvincible;

    // 보스 등장 / 하울링 / 처치 등 연출 중 무적 (피격 후 무적과 별개로 유지)
    private bool _isCinematicInvincible;
    [SerializeField, ReadOnly] private bool _isRespawnProtected;

    // 가드 / 패링으로 막은 직후 잠깐 무적 (공격의 AttackDefense.BlockInvincibleTime)
    private float _blockInvincibleUntil = float.NegativeInfinity;
    public bool IsInvincible => _isInvincible || _isCinematicInvincible || _isRespawnProtected;

    public HitData LastFatalHitData { get; private set; }

    private const string DefaultKillReason = "처형 / 즉사";

    private SpriteRenderer _spriteRenderer;
    private Coroutine _invincibleRoutine;
    private PlayerGuard _guard;
    private Player _player;

    public event Action<float, float> OnHealthChanged;
    public event Action OnDied;
    public event Action OnPotionUsed;
    public event Action<HitData> OnHit;
    public event Action<int> OnPotionChanged;
    public event Action<HitData> OnDodgeSuccess;


    private void Awake()
    {
        _currentHealth = _maxHealth;
        _potionCount = _maxPotionCount;
        _isDead = false;
        _isRespawnProtected = false;
        LastFatalHitData = default;
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // 독 걸렸을 때 캐릭터 초록빛 연출 (프리팹에 없으면 자동으로 붙임)
        if (!TryGetComponent(out PlayerPoisonVisual _))
        {
            gameObject.AddComponent<PlayerPoisonVisual>();
        }
    }

    private void Start()
    {
        // Player.Awake에서 PlayerGuard를 추가하는 경우가 있어서 Awake가 아닌 Start에서 가져옴
        TryGetComponent(out _guard);
        TryGetComponent(out _player);
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
    }

    public bool TakeDamage(HitData hitdata)
    {
        // 가드, 피격 이벤트보다 먼저 차단해야 넉백과 피격 연출도 발생하지 않는다.
        if (MapTestMode.IsEnabled) return false;

        if (IsDead) return false;

        if (_isRespawnProtected)
        {
            return false;
        }

        if (_isInvincible || _isCinematicInvincible)
        {
            if (_isInvincible && _player != null && _player.StateMachine != null && _player.StateMachine.CurrentState is PlayerDodgeState)
            {
                OnDodgeSuccess?.Invoke(hitdata);
            }

            return false;
        }

        // 가드 / 패링으로 막은 직후 잠깐은 후속 공격에 맞지 않음 (가드 게이지도 깎이지 않음)
        if (Time.time < _blockInvincibleUntil) return false;

        // 가드 중이면 데미지 대신 가드 / 패링 처리
        if (_guard != null)
        {
            GuardResult guardResult = _guard.TryHandleHit(hitdata);
            if (guardResult != GuardResult.None)
            {
                // 가드(Blocked) / 패링(Parried) 모두 적용
                if (hitdata.Defense.BlockInvincibleTime > 0f)
                {
                    _blockInvincibleUntil = Mathf.Max(_blockInvincibleUntil, Time.time + hitdata.Defense.BlockInvincibleTime);
                }

                return false;
            }
        }

        GameEvents.TriggerPlayerHit(hitdata);
        ApplyHealthDamage(hitdata);
        if (_isDead) return true;

        OnHit?.Invoke(hitdata);

        // 독 공격이면 독 상태이상 (실제로 피해를 입었을 때만)
        if (hitdata.PoisonDuration > 0f)
        {
            ApplyPoison(hitdata.PoisonDuration);
        }

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

    /// <summary>환경 피해는 피격 무적과 가드를 무시하되 연출 및 리스폰 보호는 유지한다.</summary>
    public bool TakeHazardDamage(HitData hitdata)
    {
        if (MapTestMode.IsEnabled || _isDead || _isCinematicInvincible || _isRespawnProtected)
            return false;

        if (hitdata.Damage <= 0f)
            return false;

        // 연속 피해가 경직, 히트스톱 또는 일반 공격용 무적을 반복해서 만들지 않게 한다.
        ApplyHealthDamage(hitdata);
        return true;
    }

    private void ApplyHealthDamage(HitData hitdata)
    {
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
            return;
        }

        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

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

    private void Update()
    {
        UpdatePoison();

        if (_potionCooldownTimer > 0f)
        {
            _potionCooldownTimer -= Time.deltaTime;
            if (_potionCooldownTimer < 0f)
            {
                _potionCooldownTimer = 0f;
            }
        }
    }

    public void RefillPotions()
    {
        _potionCount = _maxPotionCount;
        _potionCooldownTimer = 0f;
        OnPotionChanged?.Invoke(_potionCount);
    }

    public bool TryUsePotion()
    {
        if (_isDead || _potionCount <= 0) return false;
        if (_potionCooldownTimer > 0f) return false;
        if (Mathf.Approximately(_currentHealth, _maxHealth)) return false;

        float healAmount = _maxHealth * _potionHealRatio;
        Heal(healAmount);
        _potionCount--;
        _potionCooldownTimer = _potionCooldown;
        OnPotionChanged?.Invoke(_potionCount);
        OnPotionUsed?.Invoke();
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
        ClearPoison();
        OnDied?.Invoke();
    }

    #region 독

    /// <summary>독 상태이상. 이미 걸려 있으면 남은 시간을 duration으로 다시 채움 (중첩되지 않음)</summary>
    public void ApplyPoison(float duration)
    {
        if (_isDead || duration <= 0f)
        {
            return;
        }

        bool wasPoisoned = IsPoisoned;
        _poisonRemaining = Mathf.Max(_poisonRemaining, duration);

        if (!wasPoisoned)
        {
            _poisonTickTimer = _poisonTickInterval;
            OnPoisonChanged?.Invoke(true);
        }
    }

    /// <summary>독 해제</summary>
    public void ClearPoison()
    {
        bool wasPoisoned = _poisonRemaining > 0f;
        _poisonRemaining = 0f;
        _poisonTickTimer = 0f;

        if (wasPoisoned)
        {
            OnPoisonChanged?.Invoke(false);
        }
    }

    private void UpdatePoison()
    {
        if (_poisonRemaining <= 0f)
        {
            return;
        }

        if (_isDead)
        {
            ClearPoison();
            return;
        }

        float deltaTime = Time.deltaTime;
        _poisonRemaining -= deltaTime;
        _poisonTickTimer -= deltaTime;

        if (_poisonTickTimer <= 0f)
        {
            _poisonTickTimer += _poisonTickInterval;
            PoisonTick();
        }

        if (_poisonRemaining <= 0f)
        {
            ClearPoison();
        }
    }

    /// <summary>독 피해 한 번. 무적 / 가드와 상관없이 깎이고, 피격 연출(넉백 / 무적 / 붉은 화면)은 없음</summary>
    private void PoisonTick()
    {
        if (_isDead || _isRespawnProtected || MapTestMode.IsEnabled || _poisonTickDamage <= 0f)
        {
            return;
        }

        float minHealth = _poisonCanKill ? 0f : Mathf.Min(1f, _currentHealth);
        float next = Mathf.Max(_currentHealth - _poisonTickDamage, minHealth);
        if (Mathf.Approximately(next, _currentHealth))
        {
            return;
        }

        float damage = _currentHealth - next;
        _currentHealth = next;
        OnPoisonDamaged?.Invoke(damage);
        OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

        if (_currentHealth <= 0f)
        {
            LastFatalHitData = new HitData(
                damage: _poisonTickDamage,
                knockbackForce: 0f,
                hitPoint: transform.position,
                attackDirection: Vector2.zero,
                attackName: _poisonKillReason
            );
            Die();
        }
    }

    #endregion

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

    /// <summary>연출용 무적 (반투명 표시 없음). 피격 후 무적 시간이 끝나도 풀리지 않음</summary>
    public void SetCinematicInvincible(bool isInvincible)
    {
        _isCinematicInvincible = isInvincible;
    }

    public void SetRespawnProtected(bool isProtected)
    {
        _isRespawnProtected = isProtected;
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
        ClearPoison();
        _isRespawnProtected = false;
        int playerLayer = gameObject.layer;
        int enemyAttackLayer = Constants.Layers.EnemyAttack;

        if (enemyAttackLayer != -1)
        {
            Physics2D.IgnoreLayerCollision(playerLayer, enemyAttackLayer, false);
        }
    }
}
