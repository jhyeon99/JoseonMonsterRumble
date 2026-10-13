using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 피격 또는 마무리 공격 시 화면 외곽에 비네트 펄스를 연출하는 UI 컴포넌트.
/// - 선택한 GameEvents.OnPlayerHit 또는 OnFinisher 이벤트에 반응
/// - 타임스케일(히트스톱/일시정지)에 영향받지 않도록 실제 시간(Time.unscaledDeltaTime) 기준 보간
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public class UI_HitVignette : MonoBehaviour
{
    private enum PulseTrigger
    {
        PlayerHit,
        Finisher,
    }

    [SerializeField] private PulseTrigger _trigger;
    [SerializeField, Range(0f, 1f)] private float _finisherPeakAlpha = 0.48f;

    [Header("참조 컴포넌트")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Image _vignetteImage;

    [Header("기본 연출 설정")]
    [Tooltip("비네트 펄스가 지속되는 총 시간 (초)")]
    [SerializeField, Min(0.05f)] private float _pulseDuration = 0.28f;

    [Tooltip("최대 알파 도달까지 걸리는 시간 비율 (0~1)")]
    [SerializeField, Range(0.05f, 0.5f)] private float _fadeInRatio = 0.1f;

    [Header("외곽 비네트 형태 설정")]
    [Tooltip("화면 중심부 완전 투명 영역 비율 (0~0.9)")]
    [SerializeField, Range(0f, 0.9f)] private float _innerRadiusRatio = 0.28f;

    [Tooltip("외곽 페이드 가파름 지수 (클수록 화면 끝 가장자리에만 색상 집중)")]
    [SerializeField, Range(1f, 5f)] private float _falloffSharpness = 1.5f;

    [Header("데미지 비례 투명도 설정")]
    [Tooltip("최소 피격 시 기본 불투명도")]
    [SerializeField, Range(0.05f, 1f)] private float _minAlpha = 0.45f;

    [Tooltip("최대 피격 시 한계 불투명도")]
    [SerializeField, Range(0.1f, 1f)] private float _maxAlpha = 0.85f;

    [Tooltip("최대 불투명도에 도달하는 기준 데미지")]
    [SerializeField, Min(1f)] private float _damageForMaxAlpha = 35f;

    [Header("색상 설정")]
    [SerializeField] private Color _vignetteColor = new Color(0.92f, 0.04f, 0.04f, 1f);

    [Header("저체력 위기 연출 설정")]
    [Tooltip("저체력 위험 경고가 발동하는 체력 비율 기준 (0~1)")]
    [SerializeField, Range(0.1f, 0.5f)] private float _lowHealthThreshold = 0.30f;

    [Tooltip("저체력 비네트 펄스 주기 (초)")]
    [SerializeField, Range(0.4f, 2f)] private float _heartbeatPeriod = 0.75f;

    [Tooltip("저체력 비네트 최소 불투명도")]
    [SerializeField, Range(0.05f, 0.4f)] private float _lowHealthMinAlpha = 0.15f;

    [Tooltip("저체력 비네트 최대 불투명도")]
    [SerializeField, Range(0.2f, 0.8f)] private float _lowHealthMaxAlpha = 0.55f;

    private PlayerHealth _playerHealth;
    private Coroutine _lowHealthCoroutine;
    private bool _isLowHealth;
    private Coroutine _fadeCoroutine;
    private Sprite _generatedSprite;

    private void Awake()
    {
        InitializeComponents();
    }

    private void OnEnable()
    {
        if (_trigger == PulseTrigger.Finisher)
        {
            GameEvents.OnFinisher += HandleFinisher;
        }
        else
        {
            GameEvents.OnPlayerHit += HandlePlayerHit;
            SubscribeToHealth();
        }
        ResetAlpha();
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerHit -= HandlePlayerHit;
        GameEvents.OnFinisher -= HandleFinisher;
        UnsubscribeFromHealth();
        StopVignette();
        StopLowHealthPulse();
    }

    private void OnDestroy()
    {
        if (_generatedSprite != null)
        {
            if (_generatedSprite.texture != null)
            {
                Destroy(_generatedSprite.texture);
            }
            Destroy(_generatedSprite);
        }
    }

    public void InitializeComponents()
    {
        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        if (_vignetteImage == null)
        {
            _vignetteImage = GetComponentInChildren<Image>();
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        if (_vignetteImage != null)
        {
            _vignetteImage.color = _vignetteColor;
            _vignetteImage.raycastTarget = false;

            if (_vignetteImage.sprite == null)
            {
                _vignetteImage.sprite = CreateVignetteSprite();
            }
        }
    }

    public void HandlePlayerHit(HitData hitData)
    {
        float targetAlpha = CalculateTargetAlpha(hitData.Damage);
        PlayVignette(targetAlpha, _pulseDuration);
    }

    private void HandleFinisher(Vector2 point)
    {
        PlayVignette(_finisherPeakAlpha, _pulseDuration);
    }

    public void PlayVignette(float peakAlpha, float duration)
    {
        if (_canvasGroup == null)
        {
            return;
        }

        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
        }

        _fadeCoroutine = StartCoroutine(AnimatePulse(peakAlpha, duration));
    }

    public void StopVignette()
    {
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        ResetAlpha();
    }

    public void ResetAlpha()
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
        }
    }

    private float CalculateTargetAlpha(float damage)
    {
        float damageFactor = Mathf.Clamp01(damage / _damageForMaxAlpha);
        return Mathf.Lerp(_minAlpha, _maxAlpha, damageFactor);
    }

    private IEnumerator AnimatePulse(float peakAlpha, float duration)
    {
        float fadeInDuration = Mathf.Max(0.01f, duration * _fadeInRatio);
        float fadeOutDuration = Mathf.Max(0.01f, duration - fadeInDuration);

        float startAlpha = _canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeInDuration);
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, peakAlpha, t);
            yield return null;
        }

        _canvasGroup.alpha = peakAlpha;
        elapsed = 0f;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeOutDuration);
            _canvasGroup.alpha = Mathf.Lerp(peakAlpha, 0f, t);
            yield return null;
        }

        _fadeCoroutine = null;
        if (!_isLowHealth && _canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
        }
    }

    private void SubscribeToHealth()
    {
        if (_playerHealth == null)
        {
            _playerHealth = FindFirstObjectByType<PlayerHealth>();
        }

        if (_playerHealth != null)
        {
            _playerHealth.OnHealthChanged -= HandleHealthChanged;
            _playerHealth.OnHealthChanged += HandleHealthChanged;
            HandleHealthChanged(_playerHealth.CurrentHealth, _playerHealth.MaxHealth);
        }
    }

    private void UnsubscribeFromHealth()
    {
        if (_playerHealth != null)
        {
            _playerHealth.OnHealthChanged -= HandleHealthChanged;
        }
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        float ratio = maxHealth > 0f ? (currentHealth / maxHealth) : 1f;
        bool shouldPulse = ratio <= _lowHealthThreshold && currentHealth > 0f;

        if (shouldPulse != _isLowHealth)
        {
            _isLowHealth = shouldPulse;
            if (_isLowHealth)
            {
                StartLowHealthPulse();
            }
            else
            {
                StopLowHealthPulse();
            }
        }
    }

    private void StartLowHealthPulse()
    {
        if (_lowHealthCoroutine != null)
        {
            StopCoroutine(_lowHealthCoroutine);
        }

        _lowHealthCoroutine = StartCoroutine(AnimateLowHealthPulse());
    }

    private void StopLowHealthPulse()
    {
        if (_lowHealthCoroutine != null)
        {
            StopCoroutine(_lowHealthCoroutine);
            _lowHealthCoroutine = null;
        }

        if (_canvasGroup != null && _fadeCoroutine == null)
        {
            _canvasGroup.alpha = 0f;
        }
    }

    private IEnumerator AnimateLowHealthPulse()
    {
        float elapsed = 0f;

        while (_isLowHealth)
        {
            elapsed += Time.unscaledDeltaTime;

            if (_fadeCoroutine == null && _canvasGroup != null)
            {
                float cycle = (elapsed % _heartbeatPeriod) / _heartbeatPeriod;
                float pulse;
                if (cycle < 0.22f)
                {
                    pulse = Mathf.Sin(cycle / 0.22f * Mathf.PI);
                }
                else if (cycle < 0.48f)
                {
                    pulse = Mathf.Sin((cycle - 0.22f) / 0.26f * Mathf.PI) * 0.7f;
                }
                else
                {
                    pulse = 0f;
                }

                _canvasGroup.alpha = Mathf.Lerp(_lowHealthMinAlpha, _lowHealthMaxAlpha, pulse);
            }

            yield return null;
        }

        float currentAlpha = _canvasGroup != null ? _canvasGroup.alpha : 0f;
        float fadeTime = 0.3f;
        float fadeElapsed = 0f;

        while (fadeElapsed < fadeTime)
        {
            fadeElapsed += Time.unscaledDeltaTime;
            if (_fadeCoroutine == null && _canvasGroup != null)
            {
                _canvasGroup.alpha = Mathf.Lerp(currentAlpha, 0f, fadeElapsed / fadeTime);
            }
            yield return null;
        }

        if (_fadeCoroutine == null && _canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
        }

        _lowHealthCoroutine = null;
    }

    private Sprite CreateVignetteSprite()
    {
        const int resolution = 128;
        var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        texture.name = "T_RuntimeVignette";
        texture.wrapMode = TextureWrapMode.Clamp;

        var center = new Vector2(0.5f, 0.5f);
        float maxDistance = Mathf.Sqrt(0.5f * 0.5f + 0.5f * 0.5f);

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                var uv = new Vector2((float)x / (resolution - 1), (float)y / (resolution - 1));
                float distance = Vector2.Distance(uv, center);
                float normalizedDist = Mathf.Clamp01(distance / maxDistance);

                // 내부 영역은 완전 투명(0), 외곽 경계부터 가파르게 불투명도 상승
                float alpha = 0f;
                if (normalizedDist > _innerRadiusRatio)
                {
                    float factor = (normalizedDist - _innerRadiusRatio) / (1f - _innerRadiusRatio);
                    alpha = Mathf.Pow(Mathf.Clamp01(factor), _falloffSharpness);
                }

                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        _generatedSprite = Sprite.Create(texture, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f));
        _generatedSprite.name = "SP_RuntimeVignette";
        return _generatedSprite;
    }
}
