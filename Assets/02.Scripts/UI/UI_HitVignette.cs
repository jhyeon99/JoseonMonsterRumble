using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 피격 또는 마무리 공격 시 화면 외곽에 비네트 펄스를 연출하는 UI 컴포넌트.
/// - 선택한 GameEvents.OnPlayerHit 또는 OnFinisher 이벤트에 반응
/// - 피격(PlayerHit) 모드는 저체력(붉은 심장박동)과 독(초록 심장박동)도 함께 표시
/// - 붉은색과 초록색이 겹치면: 피격 순간은 붉은색이 덮고, 저체력 + 독이면 한 박자씩 붉은 박동 / 초록 박동이 번갈아 뜀
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

    [SerializeField]
    private PulseTrigger _trigger;

    [SerializeField, Range(0f, 1f)]
    private float _finisherPeakAlpha = 0.48f;

    [Header("참조 컴포넌트")]
    [SerializeField]
    private CanvasGroup _canvasGroup;

    [SerializeField]
    private Image _vignetteImage;

    [Header("기본 연출 설정")]
    [Tooltip("비네트 펄스가 지속되는 총 시간 (초)")]
    [SerializeField, Min(0.05f)]
    private float _pulseDuration = 0.28f;

    [Tooltip("최대 알파 도달까지 걸리는 시간 비율 (0~1)")]
    [SerializeField, Range(0.05f, 0.5f)]
    private float _fadeInRatio = 0.1f;

    [Header("외곽 비네트 형태 설정")]
    [Tooltip("화면 중심부 완전 투명 영역 비율 (0~0.9)")]
    [SerializeField, Range(0f, 0.9f)]
    private float _innerRadiusRatio = 0.28f;

    [Tooltip("외곽 페이드 가파름 지수 (클수록 화면 끝 가장자리에만 색상 집중)")]
    [SerializeField, Range(1f, 5f)]
    private float _falloffSharpness = 1.5f;

    [Header("데미지 비례 투명도 설정")]
    [Tooltip("최소 피격 시 기본 불투명도")]
    [SerializeField, Range(0.05f, 1f)]
    private float _minAlpha = 0.45f;

    [Tooltip("최대 피격 시 한계 불투명도")]
    [SerializeField, Range(0.1f, 1f)]
    private float _maxAlpha = 0.85f;

    [Tooltip("최대 불투명도에 도달하는 기준 데미지")]
    [SerializeField, Min(1f)]
    private float _damageForMaxAlpha = 35f;

    [Header("색상 설정")]
    [SerializeField]
    private Color _vignetteColor = new Color(0.92f, 0.04f, 0.04f, 1f);

    [Header("저체력 위기 연출 설정")]
    [Tooltip("저체력 위험 경고가 발동하는 체력 비율 기준 (0~1)")]
    [SerializeField, Range(0.1f, 0.5f)]
    private float _lowHealthThreshold = 0.30f;

    [Tooltip("저체력 비네트 펄스 주기 (초)")]
    [SerializeField, Range(0.4f, 2f)]
    private float _heartbeatPeriod = 0.75f;

    [Tooltip("저체력 비네트 최소 불투명도")]
    [SerializeField, Range(0.05f, 0.4f)]
    private float _lowHealthMinAlpha = 0.15f;

    [Tooltip("저체력 비네트 최대 불투명도")]
    [SerializeField, Range(0.2f, 0.8f)]
    private float _lowHealthMaxAlpha = 0.55f;

    [Header("독 연출 설정 (피격 모드에서만)")]
    [Tooltip("독 비네트 색")]
    [SerializeField]
    private Color _poisonColor = new Color(0.25f, 0.85f, 0.2f, 1f);

    [Tooltip("독 비네트 최소 불투명도")]
    [SerializeField, Range(0f, 0.4f)]
    private float _poisonMinAlpha = 0.12f;

    [Tooltip("독 비네트 최대 불투명도")]
    [SerializeField, Range(0.2f, 0.8f)]
    private float _poisonMaxAlpha = 0.5f;

    [Tooltip("독이 걸리고 풀릴 때 서서히 나타나고 사라지는 시간 (초)")]
    [SerializeField, Min(0.01f)]
    private float _poisonFadeTime = 0.3f;

    private const float LowHealthFadeTime = 0.3f;

    private PlayerHealth _playerHealth;
    private bool _isLowHealth;
    private Sprite _generatedSprite;

    // 피격 / 마무리 펄스
    private bool _pulseActive;
    private float _pulseElapsed;
    private float _pulseDurationCurrent;
    private float _pulsePeak;
    private float _pulseStartAlpha;

    // 저체력 / 독 박동 (서서히 켜고 끄는 가중치 + 박동 위상)
    private float _lowWeight;
    private float _lowPhase;
    private int _beatIndex;
    private float _poisonWeight;
    private float _poisonPhase;

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
        _isLowHealth = false;
        _lowWeight = 0f;
        _poisonWeight = 0f;
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

        // 지금 보이는 밝기에서 이어서 올라감 (끊김 방지)
        _pulseStartAlpha = _canvasGroup.alpha;
        _pulsePeak = peakAlpha;
        _pulseDurationCurrent = Mathf.Max(0.05f, duration);
        _pulseElapsed = 0f;
        _pulseActive = true;
    }

    public void StopVignette()
    {
        _pulseActive = false;
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

    private void Update()
    {
        if (_canvasGroup == null)
        {
            return;
        }

        float deltaTime = Time.unscaledDeltaTime;

        // 피격 / 마무리 펄스 (붉은색)
        float pulseAlpha = 0f;
        if (_pulseActive)
        {
            _pulseElapsed += deltaTime;
            pulseAlpha = EvaluatePulse();
        }

        // 저체력 박동 (붉은색)
        _lowWeight = Mathf.MoveTowards(_lowWeight, _isLowHealth ? 1f : 0f, deltaTime / LowHealthFadeTime);
        float nextLowPhase = _lowPhase + (deltaTime / _heartbeatPeriod);
        if (nextLowPhase >= 1f)
        {
            _beatIndex++;
        }

        _lowPhase = Mathf.Repeat(nextLowPhase, 1f);

        // 독 (초록색)
        bool poisoned = _playerHealth != null && _playerHealth.IsPoisoned;
        _poisonWeight = Mathf.MoveTowards(_poisonWeight, poisoned ? 1f : 0f, deltaTime / _poisonFadeTime);
        _poisonPhase = Mathf.Repeat(_poisonPhase + (deltaTime / _heartbeatPeriod), 1f);

        // 저체력 + 독이면 한 박자씩 번갈아 (짝수 박자 = 붉은색, 홀수 박자 = 초록색).
        // 한쪽만 있으면 그 색이 매 박자 뜀. 켜지고 꺼질 때는 가중치로 자연스럽게 넘어감
        bool redTurn = (_beatIndex & 1) == 0;
        float redShare = Mathf.Lerp(1f, redTurn ? 1f : 0f, _poisonWeight);
        float greenShare = Mathf.Lerp(1f, redTurn ? 0f : 1f, _lowWeight);

        float lowAlpha = _lowWeight > 0f
            ? _lowWeight * redShare * Mathf.Lerp(_lowHealthMinAlpha, _lowHealthMaxAlpha, Heartbeat(_lowPhase))
            : 0f;

        float poisonAlpha = 0f;
        if (_poisonWeight > 0f)
        {
            // 혼자일 때는 자기 위상, 저체력과 함께면 저체력 박동에 맞춰 같은 박자에 뜀
            float beat = Mathf.Lerp(Heartbeat(_poisonPhase), Heartbeat(_lowPhase), _lowWeight);
            poisonAlpha = _poisonWeight * greenShare * Mathf.Lerp(_poisonMinAlpha, _poisonMaxAlpha, beat);
        }

        float red = Mathf.Max(pulseAlpha, lowAlpha);
        float green = poisonAlpha;

        // 두 색이 겹치면 밝기는 자연스럽게 더해지고(스크린 합성), 색은 더 강한 쪽으로 기움
        float alpha = 1f - ((1f - red) * (1f - green));
        alpha = Mathf.Min(alpha, Mathf.Max(_maxAlpha, _poisonMaxAlpha, _lowHealthMaxAlpha, _finisherPeakAlpha));
        _canvasGroup.alpha = alpha;

        if (_vignetteImage != null)
        {
            float total = red + green;
            float greenRatio = total > 0.0001f ? green / total : 0f;
            // 피격 펄스가 강하게 들어오면 붉은색이 확실히 이기도록 비율을 한 번 더 기울임
            greenRatio = Mathf.SmoothStep(0f, 1f, greenRatio);
            var color = Color.Lerp(_vignetteColor, _poisonColor, greenRatio);
            color.a = 1f;
            _vignetteImage.color = color;
        }
    }

    /// <summary>피격 펄스: 지금 밝기에서 최대까지 빠르게 올랐다가 서서히 사라짐</summary>
    private float EvaluatePulse()
    {
        float fadeInDuration = Mathf.Max(0.01f, _pulseDurationCurrent * _fadeInRatio);
        float fadeOutDuration = Mathf.Max(0.01f, _pulseDurationCurrent - fadeInDuration);

        if (_pulseElapsed < fadeInDuration)
        {
            return Mathf.Lerp(_pulseStartAlpha, _pulsePeak, _pulseElapsed / fadeInDuration);
        }

        float t = (_pulseElapsed - fadeInDuration) / fadeOutDuration;
        if (t >= 1f)
        {
            _pulseActive = false;
            return 0f;
        }

        return Mathf.Lerp(_pulsePeak, 0f, t);
    }

    /// <summary>심장박동 모양 (0~1 위상 → 0~1 세기): 쿵-쿵 두 번 뛰고 쉼</summary>
    private static float Heartbeat(float cycle)
    {
        if (cycle < 0.22f)
        {
            return Mathf.Sin(cycle / 0.22f * Mathf.PI);
        }

        if (cycle < 0.48f)
        {
            return Mathf.Sin((cycle - 0.22f) / 0.26f * Mathf.PI) * 0.7f;
        }

        return 0f;
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
        _isLowHealth = ratio <= _lowHealthThreshold && currentHealth > 0f;
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