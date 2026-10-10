using System;
using UnityEngine;

[Serializable]
public struct EnemyHitFeedbackSettings
{
    [Min(0.01f)] public float FlashDuration;
    [Range(0f, 1f)] public float PeakOpacity;

    public EnemyHitFeedbackSettings(float flashDuration, float peakOpacity)
    {
        FlashDuration = flashDuration;
        PeakOpacity = peakOpacity;
    }
}

[DisallowMultipleComponent]
public sealed class EnemyHitFeedback : MonoBehaviour
{
    private enum FeedbackState
    {
        Idle,
        Reacting,
    }

    [SerializeField] private EnemyHealth _health;

    [Tooltip("보스에 쓸 때는 Health 대신 여기에 BossHealth 연결 (둘 중 하나만 있으면 됨)")]
    [SerializeField] private BossHealth _bossHealth;

    // Health / Boss Health가 둘 다 비어 있으면 부모에서 찾는 피격 신호 (올챙이, 독 방울 등)
    private IHitFeedbackSource _feedbackSource;
    [SerializeField] private SpriteRenderer _sourceRenderer;
    [SerializeField] private SpriteRenderer _flashRenderer;

    [Header("피격 반응")]
    [SerializeField] private EnemyHitFeedbackSettings _lightHit = new EnemyHitFeedbackSettings(0.16f, 0.65f);
    [SerializeField] private EnemyHitFeedbackSettings _heavyHit = new EnemyHitFeedbackSettings(0.2f, 0.85f);
    [SerializeField] private EnemyHitFeedbackSettings _talismanHit = new EnemyHitFeedbackSettings(0.1f, 0.35f);
    [SerializeField] private Color _flashColor = Color.white;

    private const float FlashRiseRatio = 0.15f;
    private const int FlashSortingOffset = 1;

    private FeedbackState _state;
    private EnemyHitFeedbackSettings _activeSettings;
    private float _elapsed;

    private void Awake()
    {
        if (_health == null && _bossHealth == null)
        {
            _feedbackSource = GetComponentInParent<IHitFeedbackSource>();
        }

        if ((_health == null && _bossHealth == null && _feedbackSource == null) || _sourceRenderer == null || _flashRenderer == null ||
            _sourceRenderer.sprite == null || _flashRenderer.sharedMaterial == null)
        {
            Debug.LogError($"{name}: EnemyHitFeedback의 Health, Renderer, Sprite 또는 Material 참조가 없습니다.", this);
            enabled = false;
            return;
        }

        ResetFeedback();
    }

    private void OnEnable()
    {
        if (_health != null)
        {
            _health.OnHit += HandleHit;
            _health.OnDeath += ResetFeedback;
        }

        if (_bossHealth != null)
        {
            _bossHealth.OnDamaged += HandleHit;
        }

        if (_feedbackSource != null)
        {
            _feedbackSource.OnHitFeedback += HandleHit;
        }
    }

    private void OnDisable()
    {
        if (_health != null)
        {
            _health.OnHit -= HandleHit;
            _health.OnDeath -= ResetFeedback;
        }

        if (_bossHealth != null)
        {
            _bossHealth.OnDamaged -= HandleHit;
        }

        if (_feedbackSource != null)
        {
            _feedbackSource.OnHitFeedback -= HandleHit;
        }

        ResetFeedback();
    }

    private void HandleHit(HitData hitData)
    {
        _activeSettings = hitData.IsTalisman
            ? _talismanHit
            : hitData.IsHeavyImpact ? _heavyHit : _lightHit;
        _elapsed = 0f;
        _state = FeedbackState.Reacting;

        UpdateFlash();
    }

    private void LateUpdate()
    {
        if (_state != FeedbackState.Reacting || PauseController.IsPaused)
        {
            return;
        }

        // 히트스탑 중에도 강조는 진행하되 일시정지에서는 위 조건으로 멈춘다.
        _elapsed += Time.unscaledDeltaTime;
        UpdateFlash();

        if (_elapsed >= _activeSettings.FlashDuration)
        {
            ResetFeedback();
        }
    }

    private void UpdateFlash()
    {
        _flashRenderer.enabled = _elapsed < _activeSettings.FlashDuration && _sourceRenderer.enabled;
        if (!_flashRenderer.enabled)
        {
            return;
        }

        _flashRenderer.sprite = _sourceRenderer.sprite;
        _flashRenderer.flipX = _sourceRenderer.flipX;
        _flashRenderer.flipY = _sourceRenderer.flipY;
        _flashRenderer.sortingLayerID = _sourceRenderer.sortingLayerID;
        _flashRenderer.sortingOrder = _sourceRenderer.sortingOrder + FlashSortingOffset;
        _flashRenderer.maskInteraction = _sourceRenderer.maskInteraction;

        Color color = _flashColor;
        float progress = Mathf.Clamp01(_elapsed / _activeSettings.FlashDuration);
        float opacity = progress < FlashRiseRatio
            ? Mathf.SmoothStep(0f, 1f, progress / FlashRiseRatio)
            : Mathf.SmoothStep(1f, 0f, (progress - FlashRiseRatio) / (1f - FlashRiseRatio));
        color.a *= _sourceRenderer.color.a * _activeSettings.PeakOpacity * opacity;
        _flashRenderer.color = color;
    }

    private void ResetFeedback()
    {
        _state = FeedbackState.Idle;
        _elapsed = 0f;
        _activeSettings = default;

        if (_flashRenderer != null)
        {
            _flashRenderer.enabled = false;
        }
    }
}
