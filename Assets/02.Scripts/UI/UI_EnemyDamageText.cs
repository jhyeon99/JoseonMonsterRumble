using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class UI_EnemyDamageText : MonoBehaviour
{
    private const float MinimumDuration = 0.01f;
    private const string DamageTextName = "Enemy Damage";
    private const string DamageFormat = "0";
    private const string HealFormat = "+0";
    private const string MissingCanvasWarning = "UI_EnemyDamageText requires a parent Canvas.";
    private const string MissingFontWarning = "UI_EnemyDamageText: damage font or material is missing.";
    private const string MissingPlayerHealthWarning = "UI_EnemyDamageText: PlayerHealth is missing for player damage display.";
    private const float MinimumSizeMultiplier = 0.1f;
    private const float MaximumFadeStart = 0.99f;
    private const int HeavyComboStep = 3;
    private const float PopPeakRatio = 0.3f;
    private static readonly Vector3 CompressedScale = new Vector3(1.06f, 0.72f, 1f);
    private static readonly Vector2 TextArea = new Vector2(240f, 100f);
    [SerializeField] private Camera _worldCamera;
    [SerializeField] private TextMeshProUGUI _textPrefab;
    [SerializeField] private TMP_FontAsset _damageFont;
    [SerializeField] private Material _damageMaterial;
    [SerializeField] private TMP_FontAsset _finisherFont;
    [SerializeField] private Material _finisherMaterial;
    [SerializeField, Min(0.01f)] private float _duration = 0.8f;
    [SerializeField, Min(MinimumDuration)] private float _popDuration = 0.12f;
    [SerializeField, Min(1f)] private float _popScale = 1.18f;
    [Header("숫자 정지와 회복 표시")]
    [SerializeField, Min(0f)] private float _impactHoldDuration = 0.075f;
    [SerializeField, Min(1f)] private float _impactPeakScale = 1.3f;
    [SerializeField, Min(0f)] private float _healHoldDuration = 0.15f;
    [SerializeField, Min(1f)] private float _healPeakScale = 1.2f;
    [SerializeField, Min(0.01f)] private float _healDuration = 1f;
    [SerializeField] private Vector2 _healOffset = new Vector2(0f, 75f);
    [SerializeField, Range(0f, 0.99f)] private float _fadeStart = 0.55f;
    [SerializeField, Min(0f)] private float _laneSpacing = 12f;
    [SerializeField, Min(0f)] private float _horizontalTravel = 26f;
    [SerializeField, Min(1f)] private float _heavyMotionMultiplier = 1.3f;
    [SerializeField, Min(1f)] private float _finisherMotionMultiplier = 1.5f;
    [SerializeField] private float _riseDistance = 45f;
    [SerializeField] private Vector2 _offset = new Vector2(10f, 40f);
    [SerializeField] private Vector2 _finisherOffset = new Vector2(40f, -24f);
    [SerializeField, Min(1f)] private float _finisherSizeMultiplier = 2f;
    [SerializeField] private Color _textColor = new Color(1f, 0.96f, 0.86f);
    [SerializeField] private Color _playerDamageColor = new Color(1f, 0.22f, 0.18f, 1f);
    [SerializeField] private Color _playerHealColor = new Color(0.18f, 0.88f, 0.38f, 1f);
    [Header("독 데미지 표시")]
    [Tooltip("독으로 깎인 숫자 색 (회복 초록과 구분되는 탁한 연두)")]
    [SerializeField] private Color _poisonDamageColor = new Color(0.62f, 0.86f, 0.18f, 1f);
    [Tooltip("독 숫자 크기 배율 (일반 피격보다 작게)")]
    [SerializeField, Range(0.3f, 1.5f)] private float _poisonSizeMultiplier = 0.8f;
    [Tooltip("플레이어 몸 중심 기준 표시 위치 보정 (월드)")]
    [SerializeField] private Vector2 _poisonWorldOffset = new Vector2(0f, 0.3f);
    [SerializeField, Min(1f)] private float _strongPlayerHitMotionMultiplier = 1.45f;
    [SerializeField] private Color _finisherColor = new Color(1f, 0.75f, 0.18f);
    [SerializeField] private Color _buffedColor = new Color(0.35f, 0.85f, 1.0f, 1.0f);
    [SerializeField, Min(1f)] private float _fontSize = 56f;
    [SerializeField] private float _characterSpacing = -3f;
    [SerializeField] private float _finisherCharacterSpacing = -3f;
    [Header("데미지에 따른 크기")]
    [SerializeField, Min(0f)] private float _minSizeDamage = 5f;
    [SerializeField, Range(0.1f, 1f)] private float _minSizeMultiplier = 0.85f;
    [Tooltip("이 데미지는 기본 크기로 표시합니다.")]
    [SerializeField, Min(0f)]
    private float _baseSizeDamage = 15f;
    [Tooltip("이 데미지 이상에서는 최대 배율로 표시합니다.")]
    [SerializeField, Min(0.01f)]
    private float _maxSizeDamage = 50f;
    [SerializeField, Min(1f)] private float _maxSizeMultiplier = 1.25f;

    private Canvas _canvas;
    private PlayerHealth _playerHealth;
    private float _lastPlayerHealth = -1f;
    private PlayerPotionAura _potionAura;
    private Player _player;
    private float _pendingHealAmount;
    private RectTransform _container;
    private Transform _finisherTarget;
    private EnemyHealth _finisherEnemyHealth;
    private BossHealth _finisherBossHealth;
    private Collider2D _finisherTargetCollider;
    private Collider2D _finisherAttackerCollider;
    private Vector3 _finisherHitPoint;
    private float _finisherDamage;
    private float _nextLaneSign = 1f;
    private readonly List<TextMeshProUGUI> _activeTexts = new List<TextMeshProUGUI>();

    private void Awake()
    {
        _container = (RectTransform)transform;
        _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null)
        {
            Debug.LogWarning(MissingCanvasWarning, this);
            enabled = false;
        }
        if (_damageFont == null || _damageMaterial == null)
        {
            Debug.LogWarning(MissingFontWarning, this);
        }
    }

    private void Start()
    {
        SubscribePlayerHealth();
    }

    private void OnEnable()
    {
        GameEvents.OnEnemyHit += HandleEnemyHit;
        GameEvents.OnPlayerHit += HandlePlayerHit;
        GameEvents.OnFinisherAnticipation += HandleFinisherAnticipation;
        GameEvents.OnFinisherAnticipationCancelled += ResetFinisher;
        GameEvents.OnFinisherImpact += HandleFinisherImpact;
        SubscribePlayerHealth();
    }

    private void OnDisable()
    {
        GameEvents.OnEnemyHit -= HandleEnemyHit;
        GameEvents.OnPlayerHit -= HandlePlayerHit;
        GameEvents.OnFinisherAnticipation -= HandleFinisherAnticipation;
        GameEvents.OnFinisherAnticipationCancelled -= ResetFinisher;
        GameEvents.OnFinisherImpact -= HandleFinisherImpact;
        UnsubscribePlayerHealth();
        ResetFinisher();
        _nextLaneSign = 1f;
        StopAllCoroutines();
        foreach (TextMeshProUGUI text in _activeTexts)
        {
            if (text != null)
            {
                Destroy(text.gameObject);
            }
        }

        _activeTexts.Clear();
    }

    private void SubscribePlayerHealth()
    {
        if (_playerHealth == null)
        {
            _playerHealth = FindFirstObjectByType<PlayerHealth>();
        }

        if (_playerHealth != null)
        {
            _playerHealth.OnHealthChanged -= HandlePlayerHealthChanged;
            _playerHealth.OnHealthChanged += HandlePlayerHealthChanged;
            _playerHealth.OnPoisonDamaged -= HandlePoisonDamaged;
            _playerHealth.OnPoisonDamaged += HandlePoisonDamaged;
            _lastPlayerHealth = _playerHealth.CurrentHealth;
            _potionAura = _playerHealth.GetComponent<PlayerPotionAura>();
            _player = _playerHealth.GetComponent<Player>();
            if (_potionAura != null)
            {
                _potionAura.OnRecoveryPulse -= HandleRecoveryPulse;
                _potionAura.OnRecoveryPulse += HandleRecoveryPulse;
            }
        }
    }

    private void UnsubscribePlayerHealth()
    {
        if (_potionAura != null)
        {
            _potionAura.OnRecoveryPulse -= HandleRecoveryPulse;
            _potionAura = null;
        }
        _pendingHealAmount = 0f;
        _player = null;
        if (_playerHealth != null)
        {
            _playerHealth.OnHealthChanged -= HandlePlayerHealthChanged;
            _playerHealth.OnPoisonDamaged -= HandlePoisonDamaged;
            _playerHealth = null;
        }

        _lastPlayerHealth = -1f;
    }

    private void HandlePlayerHealthChanged(float currentHealth, float maxHealth)
    {
        if (_lastPlayerHealth < 0f)
        {
            _lastPlayerHealth = currentHealth;
            return;
        }

        float healAmount = currentHealth - _lastPlayerHealth;
        _lastPlayerHealth = currentHealth;

        if (healAmount > 0.01f && _playerHealth != null && !_playerHealth.IsDead)
        {
            if (_potionAura != null && _player != null && _player.StateMachine.CurrentState == _player.PotionState &&
                (_potionAura.IsWaitingForSip || _playerHealth.PotionCooldownTimer <= 0f))
            {
                _pendingHealAmount += healAmount;
                return;
            }
            ShowDamage(healAmount, _playerHealth.transform.position, false, Vector2.up, 1f, isPlayerHeal: true);
        }
        if (currentHealth <= 0f)
        {
            _pendingHealAmount = 0f;
        }
    }

    private void HandleRecoveryPulse()
    {
        float healAmount = _pendingHealAmount;
        _pendingHealAmount = 0f;
        if (healAmount > 0f && _playerHealth != null && !_playerHealth.IsDead)
        {
            ShowDamage(healAmount, _playerHealth.transform.position, false, Vector2.up, 1f, isPlayerHeal: true);
        }
    }

    private void HandleEnemyHit(HitData hitData)
    {
        float motionMultiplier = hitData.ComboStep == HeavyComboStep ? _heavyMotionMultiplier : 1f;
        ShowDamage(hitData.Damage, hitData.HitPoint, false, hitData.AttackDirection, motionMultiplier,
            hitData.IsTalisman, hitData.IsBuffed);
    }

    private void HandlePlayerHit(HitData hitData)
    {
        if (_playerHealth == null)
        {
            SubscribePlayerHealth();
        }
        if (_playerHealth == null)
        {
            Debug.LogWarning(MissingPlayerHealthWarning, this);
            return;
        }

        // OnPlayerHit is raised after defence checks and before HP is reduced.
        float damage = Mathf.Min(Mathf.Max(0f, hitData.Damage), _playerHealth.CurrentHealth);
        float hitStrength = Mathf.InverseLerp(_baseSizeDamage, Mathf.Max(_baseSizeDamage + MinimumDuration, _maxSizeDamage), hitData.Damage);
        float motionMultiplier = Mathf.Lerp(1f, _strongPlayerHitMotionMultiplier, hitStrength);
        ShowDamage(damage, hitData.HitPoint, false, hitData.AttackDirection, motionMultiplier,
            isPlayerDamage: true);
    }

    /// <summary>독 데미지: 플레이어 몸 위에 작은 연두색 숫자</summary>
    private void HandlePoisonDamaged(float damage)
    {
        if (_playerHealth == null || damage <= 0f)
        {
            return;
        }

        Vector3 position = _playerHealth.transform.position;
        if (_playerHealth.TryGetComponent(out Collider2D body) && body.enabled)
        {
            position = body.bounds.center;
        }

        position += (Vector3)_poisonWorldOffset;
        ShowDamage(damage, position, false, Vector2.zero, 1f, isPoison: true);
    }

    private void HandleFinisherAnticipation(Transform attacker, Transform target, float progress)
    {
        if (target == null)
        {
            ResetFinisher();
            return;
        }
        if (target != _finisherTarget)
        {
            ResetFinisher();
            _finisherTarget = target;
            target.TryGetComponent(out _finisherEnemyHealth);
            target.TryGetComponent(out _finisherBossHealth);
            target.TryGetComponent(out _finisherTargetCollider);
            if (attacker != null)
            {
                attacker.TryGetComponent(out _finisherAttackerCollider);
            }
        }
        Vector3 attackerCenter = _finisherAttackerCollider != null
            ? _finisherAttackerCollider.bounds.center
            : attacker != null ? attacker.position : target.position;
        // Sample contact before defeat can disable or move the target's collider.
        _finisherHitPoint = _finisherTargetCollider != null && _finisherTargetCollider.enabled
            ? (Vector3)_finisherTargetCollider.ClosestPoint(attackerCenter)
            : target.position;
        // Impact is reported after instant defeat; preserve HP before it becomes zero.
        if (_finisherEnemyHealth != null)
        {
            _finisherDamage = _finisherEnemyHealth.CurrentHp;
        }
        else if (_finisherBossHealth != null)
        {
            _finisherDamage = _finisherBossHealth.CurrentHp;
        }
    }

    private void HandleFinisherImpact(Transform attacker, Transform target)
    {
        if (target != null && target == _finisherTarget)
        {
            Vector2 direction = attacker != null ? _finisherHitPoint - attacker.position : Vector3.zero;
            ShowDamage(_finisherDamage, _finisherHitPoint, true, direction, _finisherMotionMultiplier);
        }
        ResetFinisher();
    }

    private void ResetFinisher()
    {
        _finisherTarget = null;
        _finisherEnemyHealth = null;
        _finisherBossHealth = null;
        _finisherTargetCollider = null;
        _finisherAttackerCollider = null;
        _finisherHitPoint = Vector3.zero;
        _finisherDamage = 0f;
    }

    private void ShowDamage(float damage, Vector3 position, bool isFinisher,
        Vector2 attackDirection, float motionMultiplier, bool isTalisman = false, bool isBuffed = false,
        bool isPlayerDamage = false, bool isPlayerHeal = false, bool isPoison = false)
    {
        if (_canvas == null || damage <= 0f)
        {
            return;
        }
        Camera worldCamera = _worldCamera != null ? _worldCamera : Camera.main;
        if (worldCamera == null || worldCamera.WorldToScreenPoint(position).z <= 0f)
        {
            return;
        }

        TextMeshProUGUI text;
        if (_textPrefab != null)
        {
            text = Instantiate(_textPrefab, _container, false);
        }
        else
        {
            var textObject = new GameObject(DamageTextName, typeof(RectTransform));
            textObject.transform.SetParent(_container, false);
            text = textObject.AddComponent<TextMeshProUGUI>();
        }

        bool usesYellowFont = isFinisher || isTalisman;
        TMP_FontAsset font = usesYellowFont && _finisherFont != null ? _finisherFont : _damageFont;
        Material material = usesYellowFont && _finisherMaterial != null ? _finisherMaterial : _damageMaterial;
        if (font != null)
        {
            text.font = font;
        }
        if (material != null)
        {
            text.fontSharedMaterial = material;
        }
        text.fontSize = _fontSize;
        text.characterSpacing = usesYellowFont ? _finisherCharacterSpacing : _characterSpacing;
        text.enableAutoSizing = false;

        Color targetColor;
        if (isPoison)
        {
            targetColor = _poisonDamageColor;
        }
        else if (isPlayerHeal)
        {
            targetColor = _playerHealColor;
        }
        else if (isPlayerDamage)
        {
            targetColor = _playerDamageColor;
        }
        else if (usesYellowFont)
        {
            targetColor = _finisherColor;
        }
        else if (isBuffed)
        {
            targetColor = _buffedColor;
        }
        else
        {
            targetColor = _textColor;
        }
        text.color = targetColor;
        text.alignment = TextAlignmentOptions.Center;
        text.rectTransform.sizeDelta = TextArea;

        float baseDamage = Mathf.Max(0f, _baseSizeDamage);
        float minDamage = Mathf.Clamp(_minSizeDamage, 0f, baseDamage);
        float maxDamage = Mathf.Max(baseDamage + MinimumDuration, _maxSizeDamage);
        float sizeMultiplier = damage < baseDamage
            ? Mathf.Lerp(Mathf.Clamp(_minSizeMultiplier, MinimumSizeMultiplier, 1f), 1f,
                Mathf.InverseLerp(minDamage, baseDamage, damage))
            : Mathf.Lerp(1f, Mathf.Max(1f, _maxSizeMultiplier),
                Mathf.InverseLerp(baseDamage, maxDamage, damage));
        float finisherSize = isFinisher ? Mathf.Max(1f, _finisherSizeMultiplier) : 1f;
        float poisonSize = isPoison ? _poisonSizeMultiplier : 1f;
        Vector3 baseScale = Vector3.one * (sizeMultiplier * finisherSize * poisonSize);
        text.text = damage.ToString(isPlayerHeal ? HealFormat : DamageFormat);
        text.raycastTarget = false;
        text.gameObject.SetActive(true);
        _activeTexts.Add(text);
        float lateralOffset = isFinisher || isPlayerHeal ? 0f : _nextLaneSign * _laneSpacing;
        if (!isFinisher && !isPlayerHeal)
        {
            _nextLaneSign = -_nextLaneSign;
        }
        float horizontalDirection = Mathf.Approximately(attackDirection.x, 0f) ? 0f : Mathf.Sign(attackDirection.x);
        StartCoroutine(AnimateDamage(text, position, worldCamera, baseScale, lateralOffset,
            horizontalDirection, Mathf.Max(1f, motionMultiplier), isFinisher, isPlayerHeal));
    }

    private IEnumerator AnimateDamage(TextMeshProUGUI text, Vector3 position, Camera worldCamera,
        Vector3 baseScale, float lateralOffset, float horizontalDirection, float motionMultiplier,
        bool isFinisher, bool isPlayerHeal)
    {
        float elapsed = 0f;
        float popElapsed = 0f;
        float duration = Mathf.Max(MinimumDuration, isPlayerHeal ? _healDuration : _duration);
        float popDuration = Mathf.Clamp(_popDuration, MinimumDuration, duration);
        float peakTime = popDuration * PopPeakRatio;
        float requestedPeak = isPlayerHeal ? _healPeakScale : Mathf.Max(_popScale, _impactPeakScale);
        float peakScale = 1f + (Mathf.Max(1f, requestedPeak) - 1f) * motionMultiplier;
        float holdDuration = Mathf.Max(0f, isPlayerHeal ? _healHoldDuration : _impactHoldDuration);
        float departureTime = peakTime + holdDuration;
        duration += holdDuration;
        float fadeStart = Mathf.Clamp(_fadeStart, 0f, MaximumFadeStart);
        Color color = text.color;
        Vector2 offset = isPlayerHeal ? _healOffset : isFinisher ? _finisherOffset : _offset;
        Canvas root = _canvas.rootCanvas;
        Camera uiCamera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
        while (elapsed < duration && text != null && worldCamera != null)
        {
            float progress = elapsed / duration;
            Vector3 screenPosition = worldCamera.WorldToScreenPoint(position);
            if (screenPosition.z <= 0f)
            {
                break;
            }
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _container, screenPosition, uiCamera, out Vector2 localPosition))
            {
                float travelProgress = Mathf.Clamp01((elapsed - departureTime) / Mathf.Max(MinimumDuration, duration - departureTime));
                float riseProgress = isPlayerHeal ? Mathf.SmoothStep(0f, 1f, travelProgress)
                    : 1f - (1f - travelProgress) * (1f - travelProgress) * (1f - travelProgress);
                text.rectTransform.localPosition = localPosition + offset +
                    new Vector2(lateralOffset + horizontalDirection * _horizontalTravel * motionMultiplier * riseProgress,
                        _riseDistance * riseProgress);
            }

            Vector3 popScale;
            if (popElapsed < peakTime)
            {
                float expansion = Mathf.Clamp01(popElapsed / peakTime);
                Vector3 initialScale = isPlayerHeal ? Vector3.one : CompressedScale;
                popScale = Vector3.Lerp(initialScale, Vector3.one * peakScale,
                    1f - (1f - expansion) * (1f - expansion));
            }
            else if (popElapsed < departureTime)
            {
                popScale = Vector3.one * peakScale;
            }
            else
            {
                float settling = Mathf.Clamp01((popElapsed - departureTime) / (popDuration - peakTime));
                popScale = Vector3.one * Mathf.Lerp(peakScale, 1f,
                    1f - (1f - settling) * (1f - settling));
            }
            text.rectTransform.localScale = Vector3.Scale(baseScale, popScale);
            float alpha = 1f - Mathf.InverseLerp(fadeStart, 1f, progress);
            text.color = new Color(color.r, color.g, color.b, color.a * alpha);
            if (!PauseController.IsPaused)
            {
                // Reveal the finisher number at impact, then hold its peak during hit-stop.
                float motionDelta = isFinisher ? Time.deltaTime : Time.unscaledDeltaTime;
                elapsed += motionDelta;
                popElapsed = isFinisher && popElapsed < peakTime
                    ? Mathf.Min(peakTime, popElapsed + Time.unscaledDeltaTime)
                    : popElapsed + motionDelta;
            }
            yield return null;
        }

        _activeTexts.Remove(text);
        if (text != null)
        {
            Destroy(text.gameObject);
        }
    }
}
