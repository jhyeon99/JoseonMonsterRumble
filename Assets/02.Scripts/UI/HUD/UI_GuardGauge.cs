using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UI_GuardGauge : MonoBehaviour
{
    private const float TwoPi = Mathf.PI * 2f;
    private const float ShakeCycleCount = 3f;
    private const float StrongBlockLossRatio = 0.5f;

    [System.Serializable]
    private struct FeedbackProfile
    {
        [SerializeField] private float _duration;
        [SerializeField] private float _shakeStrength;
        [SerializeField] private Color _flashColor;

        public FeedbackProfile(float duration, float shakeStrength, Color flashColor)
        {
            _duration = duration;
            _shakeStrength = shakeStrength;
            _flashColor = flashColor;
        }

        public float Duration => _duration;
        public float ShakeStrength => _shakeStrength;
        public Color FlashColor => _flashColor;
    }

    [Header("컴포넌트 참조")]
    [SerializeField] private RectTransform _feedbackRoot;
    [SerializeField] private RectTransform _fillMask;
    [SerializeField] private Image _fillMaskImage;
    [SerializeField] private Image _fillImage;
    [SerializeField] private RectTransform _ghostMask;
    [SerializeField] private Image _ghostMaskImage;
    [SerializeField] private Image _ghostImage;
    [SerializeField] private Image _feedbackFlashImage;

    [Header("고스트 설정")]
    [SerializeField, Min(0f)] private float _ghostHoldDuration = 0.12f;
    [SerializeField, Min(0.01f)] private float _ghostDecreaseDuration = 0.18f;

    [Header("피드백 설정")]
    [SerializeField]
    private FeedbackProfile _blockFeedback = new FeedbackProfile(
        0.12f,
        1.5f,
        new Color(1f, 1f, 1f, 0f));
    [SerializeField, Min(1f)] private float _strongBlockShakeMultiplier = 2f;
    [SerializeField]
    private FeedbackProfile _parryFeedback = new FeedbackProfile(
        0.1f,
        0f,
        new Color(1f, 0.3f, 0.25f, 0.35f));
    [SerializeField]
    private FeedbackProfile _breakFeedback = new FeedbackProfile(
        0.22f,
        5f,
        new Color(1f, 0.2f, 0.15f, 0.9f));
    [SerializeField]
    private FeedbackProfile _fullFeedback = new FeedbackProfile(
        0.12f,
        0f,
        new Color(0.75f, 0.95f, 1f, 0.25f));

    private Coroutine _feedbackCoroutine;
    private Coroutine _ghostCoroutine;
    private Vector2 _baseAnchoredPosition;
    private float _fillMaskFullWidth;
    private float _ghostRatio;
    private float _ghostTargetRatio;
    private float _gaugeRatioBeforeLatestChange;
    private float _previousGaugeRatio;
    private bool _hasGaugeValue;

    private void Awake()
    {
        ValidateReferences();
        CacheBaseTransform();
        InitializeFillMask();
        InitializeGhostMask();
        SetFeedbackFlashAlpha(0f);
    }

    private void OnDisable()
    {
        StopFeedback();
        StopGhostFeedback();
        _hasGaugeValue = false;
    }

    public void SetGauge(float currentGauge, float maxGauge)
    {
        if (_fillImage == null)
        {
            return;
        }

        float gaugeRatio = maxGauge > 0f
            ? Mathf.Clamp01(currentGauge / maxGauge)
            : 0f;

        _gaugeRatioBeforeLatestChange = _hasGaugeValue
            ? _previousGaugeRatio
            : gaugeRatio;

        bool hasReachedFull = _hasGaugeValue
            && _previousGaugeRatio < 1f
            && Mathf.Approximately(gaugeRatio, 1f);

        UpdateFillMask(gaugeRatio);
        UpdateGhostTarget(gaugeRatio);
        _previousGaugeRatio = gaugeRatio;
        _hasGaugeValue = true;

        if (hasReachedFull)
        {
            StartFeedback(_fullFeedback);
        }
    }

    public void PlayBlockFeedback(float ghostDuration)
    {
        StartGhostFeedback(ghostDuration);
        float lossRatio = Mathf.Max(0f, _gaugeRatioBeforeLatestChange - _previousGaugeRatio);
        float pressure = Mathf.Clamp01(lossRatio / StrongBlockLossRatio);
        float shakeMultiplier = Mathf.Lerp(1f, _strongBlockShakeMultiplier, pressure);
        var feedback = new FeedbackProfile(_blockFeedback.Duration,
            _blockFeedback.ShakeStrength * shakeMultiplier, _blockFeedback.FlashColor);
        StartFeedback(feedback);
    }

    public void PlayParryFeedback()
    {
        StartFeedback(_parryFeedback);
    }

    public void PlayBreakFeedback()
    {
        StartGhostFeedback(_ghostHoldDuration + _ghostDecreaseDuration);
        StartFeedback(_breakFeedback);
    }

    private void StartFeedback(FeedbackProfile profile)
    {
        if (_feedbackRoot == null || _feedbackFlashImage == null)
        {
            return;
        }

        StopFeedback();

        if (profile.Duration <= 0f)
        {
            return;
        }

        _feedbackCoroutine = StartCoroutine(AnimateFeedback(profile));
    }

    private IEnumerator AnimateFeedback(FeedbackProfile profile)
    {
        float elapsed = 0f;

        while (elapsed < profile.Duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float ratio = Mathf.Clamp01(elapsed / profile.Duration);
            float pulse = Mathf.Sin(ratio * Mathf.PI);

            ApplyShake(profile.ShakeStrength, ratio);
            ApplyFlash(profile.FlashColor, pulse);
            yield return null;
        }

        ResetFeedbackVisuals();
        _feedbackCoroutine = null;
    }

    private void StopFeedback()
    {
        if (_feedbackCoroutine != null)
        {
            StopCoroutine(_feedbackCoroutine);
            _feedbackCoroutine = null;
        }

        ResetFeedbackVisuals();
    }

    private void ApplyShake(float shakeStrength, float ratio)
    {
        float remainingStrength = 1f - ratio;
        float offsetX = Mathf.Sin(ratio * TwoPi * ShakeCycleCount)
            * shakeStrength
            * remainingStrength;

        _feedbackRoot.anchoredPosition = _baseAnchoredPosition + Vector2.right * offsetX;
    }

    private void ApplyFlash(Color flashColor, float pulse)
    {
        flashColor.a *= pulse;
        _feedbackFlashImage.color = flashColor;
    }

    private void CacheBaseTransform()
    {
        if (_feedbackRoot == null)
        {
            return;
        }

        _baseAnchoredPosition = _feedbackRoot.anchoredPosition;
    }

    private void InitializeFillMask()
    {
        if (_fillMask == null || _fillMaskImage == null || _fillImage == null)
        {
            return;
        }

        _fillMaskFullWidth = _fillImage.rectTransform.rect.width;
        if (_fillMaskFullWidth <= 0f)
        {
            Debug.LogWarning($"[{nameof(UI_GuardGauge)}] Fill Image 폭이 0보다 커야 합니다.", this);
            return;
        }

        _fillMask.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            _fillMaskFullWidth);
        _fillMaskImage.sprite = _fillImage.sprite;
        _fillMaskImage.type = Image.Type.Simple;
        _fillImage.type = Image.Type.Simple;
    }

    private void InitializeGhostMask()
    {
        if (_ghostMask == null || _ghostMaskImage == null || _ghostImage == null
            || _fillImage == null || _fillMaskFullWidth <= 0f)
        {
            return;
        }

        _ghostMask.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            _fillMaskFullWidth);
        _ghostImage.rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            _fillMaskFullWidth);
        _ghostMaskImage.sprite = _fillImage.sprite;
        _ghostImage.sprite = _fillImage.sprite;
        _ghostMaskImage.type = Image.Type.Simple;
        _ghostImage.type = Image.Type.Simple;
        _ghostRatio = 1f;
        _ghostTargetRatio = 1f;
    }

    private void UpdateFillMask(float gaugeRatio)
    {
        if (_fillMask == null)
        {
            return;
        }

        float targetWidth = _fillMaskFullWidth * gaugeRatio;
        _fillMask.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
    }

    private void UpdateGhostTarget(float gaugeRatio)
    {
        _ghostTargetRatio = gaugeRatio;

        if (_ghostCoroutine == null)
        {
            UpdateGhostMask(gaugeRatio);
        }
    }

    private void StartGhostFeedback(float totalDuration)
    {
        if (totalDuration <= 0f || _ghostMask == null)
        {
            return;
        }

        if (_ghostCoroutine != null)
        {
            StopCoroutine(_ghostCoroutine);
        }

        float startRatio = Mathf.Max(_ghostRatio, _gaugeRatioBeforeLatestChange);
        UpdateGhostMask(startRatio);
        _ghostCoroutine = StartCoroutine(AnimateGhostFeedback(startRatio, totalDuration));
    }

    private IEnumerator AnimateGhostFeedback(float startRatio, float totalDuration)
    {
        float holdDuration = Mathf.Min(_ghostHoldDuration, totalDuration);
        float elapsed = 0f;

        while (elapsed < holdDuration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        float decreaseDuration = totalDuration - holdDuration;
        if (decreaseDuration <= 0f)
        {
            UpdateGhostMask(_ghostTargetRatio);
            _ghostCoroutine = null;
            yield break;
        }

        elapsed = 0f;

        while (elapsed < decreaseDuration)
        {
            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / decreaseDuration);
            float ghostRatio = Mathf.SmoothStep(startRatio, _ghostTargetRatio, ratio);
            UpdateGhostMask(ghostRatio);
            yield return null;
        }

        UpdateGhostMask(_ghostTargetRatio);
        _ghostCoroutine = null;
    }

    private void StopGhostFeedback()
    {
        if (_ghostCoroutine != null)
        {
            StopCoroutine(_ghostCoroutine);
            _ghostCoroutine = null;
        }

        UpdateGhostMask(_previousGaugeRatio);
    }

    private void UpdateGhostMask(float gaugeRatio)
    {
        if (_ghostMask == null)
        {
            return;
        }

        _ghostRatio = Mathf.Clamp01(gaugeRatio);
        float targetWidth = _fillMaskFullWidth * _ghostRatio;
        _ghostMask.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
    }

    private void ResetFeedbackVisuals()
    {
        if (_feedbackRoot != null)
        {
            _feedbackRoot.anchoredPosition = _baseAnchoredPosition;
        }

        SetFeedbackFlashAlpha(0f);
    }

    private void SetFeedbackFlashAlpha(float alpha)
    {
        if (_feedbackFlashImage == null)
        {
            return;
        }

        Color color = _feedbackFlashImage.color;
        color.a = alpha;
        _feedbackFlashImage.color = color;
    }

    private void ValidateReferences()
    {
        if (_fillImage == null)
        {
            Debug.LogWarning($"[{nameof(UI_GuardGauge)}] Fill Image 참조가 필요합니다.", this);
        }

        if (_fillMask == null)
        {
            Debug.LogWarning($"[{nameof(UI_GuardGauge)}] Fill Mask 참조가 필요합니다.", this);
        }

        if (_fillMaskImage == null)
        {
            Debug.LogWarning($"[{nameof(UI_GuardGauge)}] Fill Mask Image 참조가 필요합니다.", this);
        }

        if (_ghostMask == null)
        {
            Debug.LogWarning($"[{nameof(UI_GuardGauge)}] Ghost Mask 참조가 필요합니다.", this);
        }

        if (_ghostMaskImage == null)
        {
            Debug.LogWarning($"[{nameof(UI_GuardGauge)}] Ghost Mask Image 참조가 필요합니다.", this);
        }

        if (_ghostImage == null)
        {
            Debug.LogWarning($"[{nameof(UI_GuardGauge)}] Ghost Image 참조가 필요합니다.", this);
        }

        if (_feedbackRoot == null)
        {
            Debug.LogWarning($"[{nameof(UI_GuardGauge)}] Feedback Root 참조가 필요합니다.", this);
        }

        if (_feedbackFlashImage == null)
        {
            Debug.LogWarning($"[{nameof(UI_GuardGauge)}] Feedback Flash Image 참조가 필요합니다.", this);
        }
    }
}
