using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 피격 시 화면 외곽에 붉은 비네트 펄스를 연출하는 UI 컴포넌트.
/// - GameEvents.OnPlayerHit 이벤트에 반응하여 동작
/// - 타임스케일(히트스톱/일시정지)에 영향받지 않도록 실제 시간(Time.unscaledDeltaTime) 기준 보간
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public class UI_HitVignette : MonoBehaviour
{
    [Header("참조 컴포넌트")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Image _vignetteImage;

    [Header("기본 연출 설정")]
    [Tooltip("비네트 펄스가 지속되는 총 시간 (초)")]
    [SerializeField, Min(0.05f)] private float _pulseDuration = 0.2f;

    [Tooltip("최대 알파 도달까지 걸리는 시간 비율 (0~1)")]
    [SerializeField, Range(0.05f, 0.5f)] private float _fadeInRatio = 0.15f;

    [Header("외곽 비네트 형태 설정")]
    [Tooltip("화면 중심부 완전 투명 영역 비율 (0~0.9)")]
    [SerializeField, Range(0f, 0.9f)] private float _innerRadiusRatio = 0.55f;

    [Tooltip("외곽 페이드 가파름 지수 (클수록 화면 끝 가장자리에만 붉은빛 집중)")]
    [SerializeField, Range(1f, 5f)] private float _falloffSharpness = 2.5f;

    [Header("데미지 비례 투명도 설정")]
    [Tooltip("최소 피격 시 기본 불투명도")]
    [SerializeField, Range(0.05f, 1f)] private float _minAlpha = 0.25f;

    [Tooltip("최대 피격 시 한계 불투명도")]
    [SerializeField, Range(0.1f, 1f)] private float _maxAlpha = 0.65f;

    [Tooltip("최대 불투명도에 도달하는 기준 데미지")]
    [SerializeField, Min(1f)] private float _damageForMaxAlpha = 40f;

    [Header("색상 설정")]
    [SerializeField] private Color _vignetteColor = new Color(0.85f, 0.05f, 0.05f, 1f);

    private Coroutine _fadeCoroutine;
    private Sprite _generatedSprite;

    private void Awake()
    {
        InitializeComponents();
    }

    private void OnEnable()
    {
        GameEvents.OnPlayerHit += HandlePlayerHit;
        ResetAlpha();
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerHit -= HandlePlayerHit;
        StopVignette();
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

        _canvasGroup.alpha = 0f;
        _fadeCoroutine = null;
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
