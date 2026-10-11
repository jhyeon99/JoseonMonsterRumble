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
    private const string MissingCanvasWarning = "UI_EnemyDamageText requires a parent Canvas.";
    private const string MissingFontWarning = "UI_EnemyDamageText: damage font or material is missing.";
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

    private void OnEnable()
    {
        GameEvents.OnEnemyHit += HandleEnemyHit;
        GameEvents.OnFinisherAnticipation += HandleFinisherAnticipation;
        GameEvents.OnFinisherAnticipationCancelled += ResetFinisher;
        GameEvents.OnFinisherImpact += HandleFinisherImpact;
    }

    private void OnDisable()
    {
        GameEvents.OnEnemyHit -= HandleEnemyHit;
        GameEvents.OnFinisherAnticipation -= HandleFinisherAnticipation;
        GameEvents.OnFinisherAnticipationCancelled -= ResetFinisher;
        GameEvents.OnFinisherImpact -= HandleFinisherImpact;
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

    private void HandleEnemyHit(HitData hitData)
    {
        float motionMultiplier = hitData.ComboStep == HeavyComboStep ? _heavyMotionMultiplier : 1f;
        ShowDamage(hitData.Damage, hitData.HitPoint, false, hitData.AttackDirection, motionMultiplier,
            hitData.IsTalisman, hitData.IsBuffed);
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
        Vector2 attackDirection, float motionMultiplier, bool isTalisman = false, bool isBuffed = false)
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
        if (usesYellowFont)
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
        Vector3 baseScale = Vector3.one * (sizeMultiplier * finisherSize);
        text.text = damage.ToString(DamageFormat);
        text.raycastTarget = false;
        text.gameObject.SetActive(true);
        _activeTexts.Add(text);
        float lateralOffset = isFinisher ? 0f : _nextLaneSign * _laneSpacing;
        if (!isFinisher)
        {
            _nextLaneSign = -_nextLaneSign;
        }
        float horizontalDirection = Mathf.Approximately(attackDirection.x, 0f) ? 0f : Mathf.Sign(attackDirection.x);
        StartCoroutine(AnimateDamage(text, position, worldCamera, baseScale, lateralOffset,
            horizontalDirection, Mathf.Max(1f, motionMultiplier), isFinisher));
    }

    private IEnumerator AnimateDamage(TextMeshProUGUI text, Vector3 position, Camera worldCamera,
        Vector3 baseScale, float lateralOffset, float horizontalDirection, float motionMultiplier,
        bool isFinisher)
    {
        float elapsed = 0f;
        float popElapsed = 0f;
        float duration = Mathf.Max(MinimumDuration, _duration);
        float popDuration = Mathf.Clamp(_popDuration, MinimumDuration, duration);
        float peakTime = popDuration * PopPeakRatio;
        float peakScale = 1f + (Mathf.Max(1f, _popScale) - 1f) * motionMultiplier;
        float fadeStart = Mathf.Clamp(_fadeStart, 0f, MaximumFadeStart);
        Color color = text.color;
        Vector2 offset = isFinisher ? _finisherOffset : _offset;
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
                float riseProgress = 1f - (1f - progress) * (1f - progress) * (1f - progress);
                text.rectTransform.localPosition = localPosition + offset +
                    new Vector2(lateralOffset + horizontalDirection * _horizontalTravel * motionMultiplier * riseProgress,
                        _riseDistance * riseProgress);
            }

            Vector3 popScale;
            if (popElapsed < peakTime)
            {
                float expansion = Mathf.Clamp01(popElapsed / peakTime);
                popScale = Vector3.Lerp(CompressedScale, Vector3.one * peakScale,
                    1f - (1f - expansion) * (1f - expansion));
            }
            else
            {
                float settling = Mathf.Clamp01((popElapsed - peakTime) / (popDuration - peakTime));
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
