using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 피격 시 화면 렌즈에 수묵화 그런지 브러시 자국이 튀는 화면 스플래터 연출 UI 컴포넌트.
/// - GameEvents.OnPlayerHit 이벤트에 반응하여 공격이 가해진 화면 가장자리에 브러시 스프라이트 스폰
/// - 히트스톱/타임스케일 정지에 영향받지 않도록 Time.unscaledDeltaTime 기준 보간
/// </summary>
[DisallowMultipleComponent]
public class UI_HitSplatter : MonoBehaviour
{
    [Header("스플래터 이미지 풀")]
    [SerializeField] private Image[] _splatterImages;

    [Header("브러시 스프라이트 리소스")]
    [SerializeField] private Sprite[] _splatterSprites;

    [Header("연출 색상 설정")]
    [Tooltip("스플래터 기본 색상 (흑적색 크림슨 블랙)")]
    [SerializeField] private Color _splatterColor = new Color(0.25f, 0.03f, 0.03f, 0.88f);

    [Header("시간 및 보간 설정")]
    [Tooltip("스플래터 연출 총 지속 시간 (초)")]
    [SerializeField, Min(0.05f)] private float _duration = 0.28f;

    [Tooltip("최대 알파 도달까지 걸리는 시간 (초)")]
    [SerializeField, Range(0.01f, 0.1f)] private float _fadeInDuration = 0.03f;

    [Header("크기 및 오프셋 설정")]
    [Tooltip("스플래터 크기 무작위 배율 범위")]
    [SerializeField] private Vector2 _scaleRange = new Vector2(0.9f, 1.4f);

    [Tooltip("화면 가장자리에서 중심 쪽으로 밀려 들어오는 기본 거리 (픽셀)")]
    [SerializeField, Min(0f)] private float _edgeInsetDistance = 60f;

    private int _currentPoolIndex;
    private Coroutine[] _activeCoroutines;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        GameEvents.OnPlayerHit += HandlePlayerHit;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerHit -= HandlePlayerHit;
        StopAllSplatterCoroutines();
    }

    public void Initialize()
    {
        if (_splatterImages == null || _splatterImages.Length == 0)
        {
            _splatterImages = GetComponentsInChildren<Image>(true);
        }

        _activeCoroutines = new Coroutine[_splatterImages != null ? _splatterImages.Length : 0];

        if (_splatterImages == null)
        {
            return;
        }

        for (int i = 0; i < _splatterImages.Length; i++)
        {
            if (_splatterImages[i] != null)
            {
                ResetSplatter(_splatterImages[i]);
            }
        }
    }

    private void HandlePlayerHit(HitData hitData)
    {
        if (_splatterImages == null || _splatterImages.Length == 0)
        {
            return;
        }

        StartSplatter(hitData.AttackDirection);
    }

    public void StartSplatter(Vector2 attackDirection)
    {
        if (_splatterImages == null || _splatterImages.Length == 0)
        {
            return;
        }

        int index = _currentPoolIndex;
        _currentPoolIndex = (_currentPoolIndex + 1) % _splatterImages.Length;

        Image targetImage = _splatterImages[index];
        if (targetImage == null)
        {
            return;
        }

        if (_activeCoroutines[index] != null)
        {
            StopCoroutine(_activeCoroutines[index]);
            _activeCoroutines[index] = null;
        }

        _activeCoroutines[index] = StartCoroutine(PlaySplatterRoutine(index, targetImage, attackDirection));
    }

    private IEnumerator PlaySplatterRoutine(int poolIndex, Image targetImage, Vector2 attackDirection)
    {
        SetSplatterTransform(targetImage, attackDirection);

        targetImage.gameObject.SetActive(true);
        Color baseColor = _splatterColor;
        Color startColor = baseColor;
        startColor.a = 0f;
        targetImage.color = startColor;

        float elapsed = 0f;
        while (elapsed < _fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _fadeInDuration);
            Color c = baseColor;
            c.a = Mathf.Lerp(0f, baseColor.a, t);
            targetImage.color = c;
            yield return null;
        }

        targetImage.color = baseColor;

        float fadeOutDuration = Mathf.Max(0.01f, _duration - _fadeInDuration);
        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeOutDuration);
            float alphaFactor = Mathf.Pow(1f - t, 1.5f);
            Color c = baseColor;
            c.a = baseColor.a * alphaFactor;
            targetImage.color = c;
            yield return null;
        }

        ResetSplatter(targetImage);
        _activeCoroutines[poolIndex] = null;
    }

    private void SetSplatterTransform(Image targetImage, Vector2 attackDirection)
    {
        RectTransform rect = targetImage.rectTransform;

        if (_splatterSprites != null && _splatterSprites.Length > 0)
        {
            int spriteIndex = Random.Range(0, _splatterSprites.Length);
            targetImage.sprite = _splatterSprites[spriteIndex];
        }

        Vector2 screenDir = attackDirection.sqrMagnitude > 0.01f ? attackDirection.normalized : Vector2.left;

        var anchor = new Vector2(
            Mathf.Clamp01(0.5f + screenDir.x * 0.45f),
            Mathf.Clamp01(0.5f + screenDir.y * 0.45f)
        );

        rect.anchorMin = anchor;
        rect.anchorMax = anchor;

        var randomOffset = new Vector2(
            Random.Range(-50f, 50f) - screenDir.x * _edgeInsetDistance,
            Random.Range(-50f, 50f) - screenDir.y * _edgeInsetDistance
        );
        rect.anchoredPosition = randomOffset;

        float scale = Random.Range(_scaleRange.x, _scaleRange.y);
        rect.localScale = new Vector3(scale, scale, 1f);

        float baseAngle = Mathf.Atan2(-screenDir.y, -screenDir.x) * Mathf.Rad2Deg;
        float randomAngle = baseAngle + Random.Range(-35f, 35f);
        rect.localRotation = Quaternion.Euler(0f, 0f, randomAngle);
    }

    private void ResetSplatter(Image image)
    {
        if (image == null)
        {
            return;
        }

        Color c = _splatterColor;
        c.a = 0f;
        image.color = c;
        image.gameObject.SetActive(false);
    }

    private void StopAllSplatterCoroutines()
    {
        if (_activeCoroutines == null)
        {
            return;
        }

        for (int i = 0; i < _activeCoroutines.Length; i++)
        {
            if (_activeCoroutines[i] != null)
            {
                StopCoroutine(_activeCoroutines[i]);
                _activeCoroutines[i] = null;
            }

            if (_splatterImages != null && i < _splatterImages.Length && _splatterImages[i] != null)
            {
                ResetSplatter(_splatterImages[i]);
            }
        }
    }
}
