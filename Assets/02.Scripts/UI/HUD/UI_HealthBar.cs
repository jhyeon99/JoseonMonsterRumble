using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UI_HealthBar : MonoBehaviour
{
    [Header("컴포넌트 참조")]
    [SerializeField]
    private PlayerHealth _playerHealth;

    [SerializeField]
    private RectTransform _mainMask;

    [SerializeField]
    private Image _mainMaskImage;

    [SerializeField]
    private Image _mainFill;

    [SerializeField]
    private RectTransform _ghostMask;

    [SerializeField]
    private Image _ghostMaskImage;

    [SerializeField]
    private Image _ghostFill;

    [Header("잔상 색상 설정")]
    [SerializeField]
    private Color _damageGhostColor = new Color(0.9f, 0.2f, 0.2f, 1f);

    [SerializeField]
    private Color _healGhostColor = new Color(0.2f, 0.85f, 0.3f, 1f);

    [Header("독 연출")]
    [Tooltip("독에 걸린 동안 체력바 색 (원래 색에서 이 색으로 은은하게 깜빡임)")]
    [SerializeField]
    private Color _poisonFillColor = new Color(0.45f, 0.82f, 0.2f, 1f);

    [Tooltip("독으로 깎일 때 잔상 색 (일반 피격은 붉은 잔상)")]
    [SerializeField]
    private Color _poisonGhostColor = new Color(0.6f, 0.9f, 0.25f, 1f);

    [Tooltip("체력바가 독 색으로 바뀌는 세기 (박동이 약할 때 / 강할 때)")]
    [SerializeField]
    private Vector2 _poisonFillStrength = new Vector2(0.55f, 0.9f);

    [SerializeField, Min(0.1f)]
    private float _poisonPulsePeriod = 0.9f;

    [SerializeField, Min(0.01f)]
    private float _poisonFadeTime = 0.25f;

    [Header("시간 및 보간 설정")]
    [SerializeField]
    private float _damageDelay = 0.25f;

    [SerializeField]
    private float _damageCatchupDuration = 0.35f;

    [SerializeField]
    private float _healCatchupDuration = 0.35f;

    [SerializeField]
    private float _currentRatio = 1f;

    [SerializeField]
    private float _ghostRatio = 1f;

    [Header("피격 펀치 및 셰이크 설정")]
    [SerializeField]
    private RectTransform _punchTarget;

    [SerializeField, Range(1f, 1.3f)]
    private float _punchScale = 1.08f;

    [SerializeField, Range(0f, 15f)]
    private float _punchShakeAmount = 5f;

    [SerializeField, Min(0.05f)]
    private float _punchDuration = 0.15f;

    private Coroutine _punchCoroutine;
    private Vector3 _originalScale = Vector3.one;
    private Vector2 _originalAnchoredPosition;
    private bool _hasOriginalPunchState;

    private Coroutine _trailCoroutine;
    private float _maskFullWidth;
    private Player _player;
    private PlayerPotionAura _potionAura;
    private float _pendingHealRatio = -1f;
    private float _lastActualHealth = -1f;

    private Color _mainFillOriginalColor = Color.white;
    private bool _hasMainFillColor;
    private float _poisonWeight;
    private float _poisonPhase;

    // 바로 다음 체력 변화가 독 피해인지 (PlayerHealth가 OnPoisonDamaged → OnHealthChanged 순서로 알림)
    private bool _isPoisonTick;

    private void Awake()
    {
        InitializeComponents();
        InitializePunchTarget();

        if (_mainFill != null)
        {
            _mainFillOriginalColor = _mainFill.color;
            _hasMainFillColor = true;
        }
    }

    /// <summary>독에 걸린 동안 체력바를 독 색으로 은은하게 깜빡임</summary>
    private void Update()
    {
        if (_mainFill == null || !_hasMainFillColor)
        {
            return;
        }

        bool poisoned = _playerHealth != null && _playerHealth.IsPoisoned;
        float target = poisoned ? 1f : 0f;
        if (Mathf.Approximately(_poisonWeight, target) && target == 0f)
        {
            return;
        }

        _poisonWeight = Mathf.MoveTowards(_poisonWeight, target, Time.unscaledDeltaTime / _poisonFadeTime);
        _poisonPhase = Mathf.Repeat(_poisonPhase + (Time.unscaledDeltaTime / _poisonPulsePeriod), 1f);
        float pulse = 0.5f - (0.5f * Mathf.Cos(_poisonPhase * Mathf.PI * 2f));
        float strength = _poisonWeight * Mathf.Lerp(_poisonFillStrength.x, _poisonFillStrength.y, pulse);

        var color = Color.Lerp(_mainFillOriginalColor, _poisonFillColor, strength);
        color.a = _mainFillOriginalColor.a;
        _mainFill.color = color;
    }

    private void Start()
    {
        if (_playerHealth == null)
        {
            _playerHealth = FindFirstObjectByType<PlayerHealth>();
        }

        if (_playerHealth != null)
        {
            SubscribeToHealthChanged();
            UpdateHealth(_playerHealth.CurrentHealth, _playerHealth.MaxHealth, true);
        }
    }

    private void OnEnable()
    {
        SubscribeToHealthChanged();
    }

    private void OnDisable()
    {
        UnsubscribeFromHealthChanged();

        StopTrailCoroutine();
        StopPunch();
        _pendingHealRatio = -1f;
        _lastActualHealth = -1f;

        _poisonWeight = 0f;
        if (_mainFill != null && _hasMainFillColor)
        {
            _mainFill.color = _mainFillOriginalColor;
        }
    }

    private void SubscribeToHealthChanged()
    {
        if (_playerHealth == null)
        {
            return;
        }

        _playerHealth.OnHealthChanged -= HandleHealthChanged;
        _playerHealth.OnHealthChanged += HandleHealthChanged;
        _playerHealth.OnPoisonDamaged -= HandlePoisonDamaged;
        _playerHealth.OnPoisonDamaged += HandlePoisonDamaged;
        _lastActualHealth = _playerHealth.CurrentHealth;
        _player = _playerHealth.GetComponent<Player>();
        _potionAura = _playerHealth.GetComponent<PlayerPotionAura>();
        if (_potionAura != null)
        {
            _potionAura.OnRecoveryPulse -= HandleRecoveryPulse;
            _potionAura.OnRecoveryPulse += HandleRecoveryPulse;
        }
    }

    private void UnsubscribeFromHealthChanged()
    {
        if (_potionAura != null)
        {
            _potionAura.OnRecoveryPulse -= HandleRecoveryPulse;
            _potionAura = null;
        }

        if (_playerHealth == null)
        {
            return;
        }

        _playerHealth.OnHealthChanged -= HandleHealthChanged;
        _playerHealth.OnPoisonDamaged -= HandlePoisonDamaged;
    }

    private void HandlePoisonDamaged(float damage)
    {
        _isPoisonTick = true;
    }

    private void InitializeComponents()
    {
        if (_mainFill == null)
        {
            Transform mainFillTransform = transform.Find("Bar_Fill_Mask/Bar_Fill");
            if (mainFillTransform != null)
            {
                _mainFill = mainFillTransform.GetComponent<Image>();
            }
        }

        if (_ghostFill == null)
        {
            Transform ghostTransform = transform.Find("Bar_Ghost_Mask/Bar_Ghost");
            if (ghostTransform != null)
            {
                _ghostFill = ghostTransform.GetComponent<Image>();
            }
        }

        InitializeMasks();
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        bool isPoisonTick = _isPoisonTick;
        _isPoisonTick = false;

        bool isHealing = _lastActualHealth >= 0f && currentHealth > _lastActualHealth;
        bool isDamage = _lastActualHealth >= 0f && currentHealth < _lastActualHealth;
        _lastActualHealth = currentHealth;
        if (isDamage && _pendingHealRatio >= 0f)
        {
            // 회복 표시 대기 중 피격되면 오래된 표시 대신 실제 HP에 맞춘다.
            _pendingHealRatio = -1f;
            UpdateHealth(currentHealth, maxHealth, true);
            if (!isPoisonTick)
            {
                PlayPunch();
            }

            return;
        }

        // 독 피해: 독 색 잔상, 흔들림 없음 (1초마다 깎일 때마다 흔들리면 산만함)
        if (isDamage && isPoisonTick)
        {
            ApplyPoisonDamage(currentHealth, maxHealth);
            return;
        }

        if (isHealing && _potionAura != null && _player != null &&
            _player.StateMachine.CurrentState == _player.PotionState &&
            (_potionAura.IsWaitingForSip || _playerHealth.PotionCooldownTimer <= 0f))
        {
            _pendingHealRatio = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
            return;
        }

        UpdateHealth(currentHealth, maxHealth, false);
    }

    private void HandleRecoveryPulse()
    {
        float targetRatio = _pendingHealRatio;
        _pendingHealRatio = -1f;
        if (targetRatio >= 0f && _playerHealth != null && !_playerHealth.IsDead)
        {
            UpdateHealth(_playerHealth.CurrentHealth, _playerHealth.MaxHealth, false);
        }
    }

    public void UpdateHealth(float currentHealth, float maxHealth, bool isInstant)
    {
        float targetRatio = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;

        if (isInstant)
        {
            StopTrailCoroutine();
            _currentRatio = targetRatio;
            _ghostRatio = targetRatio;

            UpdateMainMask(targetRatio);
            UpdateGhostMask(targetRatio);
            return;
        }

        if (Mathf.Approximately(targetRatio, _currentRatio))
        {
            return;
        }

        if (targetRatio < _currentRatio)
        {
            // 데미지 피격
            UpdateMainMask(targetRatio);
            StartTrailCoroutine(ApplyDamageTrail(targetRatio));
            PlayPunch();
        }
        else
        {
            // 체력 회복
            if (_ghostFill != null)
            {
                _ghostFill.color = _healGhostColor;
            }

            UpdateGhostMask(targetRatio);
            StartTrailCoroutine(ApplyHealTrail(targetRatio));
        }
    }

    private void StartTrailCoroutine(IEnumerator routine)
    {
        StopTrailCoroutine();
        _trailCoroutine = StartCoroutine(routine);
    }

    private void StopTrailCoroutine()
    {
        if (_trailCoroutine != null)
        {
            StopCoroutine(_trailCoroutine);
            _trailCoroutine = null;
        }
    }

    private IEnumerator ApplyDamageTrail(float targetRatio)
    {
        yield return ApplyDamageTrail(targetRatio, _damageGhostColor);
    }

    /// <summary>독 피해: 흔들림 없이 독 색 잔상으로 깎임</summary>
    private void ApplyPoisonDamage(float currentHealth, float maxHealth)
    {
        float targetRatio = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
        if (targetRatio >= _currentRatio)
        {
            return;
        }

        UpdateMainMask(targetRatio);
        StartTrailCoroutine(ApplyDamageTrail(targetRatio, _poisonGhostColor));
    }

    private IEnumerator ApplyDamageTrail(float targetRatio, Color ghostColor)
    {
        if (_ghostFill != null)
        {
            _ghostFill.color = ghostColor;
        }

        yield return new WaitForSecondsRealtime(_damageDelay);

        float startGhost = _ghostRatio;
        float elapsed = 0f;

        while (elapsed < _damageCatchupDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _damageCatchupDuration);
            float current = Mathf.Lerp(startGhost, targetRatio, t);

            UpdateGhostMask(current);
            yield return null;
        }

        UpdateGhostMask(targetRatio);
        _trailCoroutine = null;
    }

    private IEnumerator ApplyHealTrail(float targetRatio)
    {
        float startMain = _currentRatio;
        float elapsed = 0f;

        while (elapsed < _healCatchupDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _healCatchupDuration);
            float current = Mathf.Lerp(startMain, targetRatio, t);

            UpdateMainMask(current);
            yield return null;
        }

        UpdateMainMask(targetRatio);
        UpdateGhostMask(targetRatio);
        _trailCoroutine = null;
    }

    private void InitializeMasks()
    {
        if (_mainMask == null || _mainMaskImage == null || _mainFill == null
            || _ghostMask == null || _ghostMaskImage == null || _ghostFill == null)
        {
            Debug.LogWarning($"[{nameof(UI_HealthBar)}] HP Mask 참조가 필요합니다.", this);
            return;
        }

        _maskFullWidth = _mainFill.rectTransform.rect.width;
        if (_maskFullWidth <= 0f)
        {
            Debug.LogWarning($"[{nameof(UI_HealthBar)}] HP Fill 폭이 0보다 커야 합니다.", this);
            return;
        }

        InitializeMask(_mainMask, _mainMaskImage, _mainFill);
        InitializeMask(_ghostMask, _ghostMaskImage, _ghostFill);
        UpdateMainMask(_currentRatio);
        UpdateGhostMask(_ghostRatio);
    }

    private void InitializeMask(RectTransform mask, Image maskImage, Image fillImage)
    {
        mask.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _maskFullWidth);
        fillImage.rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            _maskFullWidth);
        maskImage.sprite = _mainFill.sprite;
        maskImage.type = Image.Type.Simple;
        fillImage.type = Image.Type.Simple;
    }

    private void UpdateMainMask(float ratio)
    {
        _currentRatio = Mathf.Clamp01(ratio);
        UpdateMaskWidth(_mainMask, _currentRatio);
    }

    private void UpdateGhostMask(float ratio)
    {
        _ghostRatio = Mathf.Clamp01(ratio);
        UpdateMaskWidth(_ghostMask, _ghostRatio);
    }

    private void UpdateMaskWidth(RectTransform mask, float ratio)
    {
        if (mask == null)
        {
            return;
        }

        float targetWidth = _maskFullWidth * ratio;
        mask.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
    }

    private void InitializePunchTarget()
    {
        if (_punchTarget == null)
        {
            _punchTarget = GetComponent<RectTransform>();
        }

        if (_punchTarget != null)
        {
            _originalScale = _punchTarget.localScale;
            _originalAnchoredPosition = _punchTarget.anchoredPosition;
            _hasOriginalPunchState = true;
        }
    }

    public void PlayPunch()
    {
        if (_punchTarget == null)
        {
            return;
        }

        if (_punchCoroutine != null)
        {
            StopCoroutine(_punchCoroutine);
            RestorePunchState();
        }

        _punchCoroutine = StartCoroutine(AnimatePunch());
    }

    public void StopPunch()
    {
        if (_punchCoroutine != null)
        {
            StopCoroutine(_punchCoroutine);
            _punchCoroutine = null;
        }

        RestorePunchState();
    }

    private void RestorePunchState()
    {
        if (_punchTarget != null && _hasOriginalPunchState)
        {
            _punchTarget.localScale = _originalScale;
            _punchTarget.anchoredPosition = _originalAnchoredPosition;
        }
    }

    private IEnumerator AnimatePunch()
    {
        float elapsed = 0f;

        while (elapsed < _punchDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _punchDuration);
            float decay = 1f - t;

            float currentScale = Mathf.Lerp(_punchScale, 1f, t);
            _punchTarget.localScale = _originalScale * currentScale;

            Vector2 shakeOffset = Random.insideUnitCircle * (_punchShakeAmount * decay);
            _punchTarget.anchoredPosition = _originalAnchoredPosition + shakeOffset;

            yield return null;
        }

        RestorePunchState();
        _punchCoroutine = null;
    }
}