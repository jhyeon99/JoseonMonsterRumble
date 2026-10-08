using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UI_GuardGauge : MonoBehaviour
{
    [Header("컴포넌트 참조")]
    [SerializeField] private Image _fillImage;
    [SerializeField] private Image _breakFlashImage;

    [Header("가드 파괴 피드백")]
    [SerializeField] private float _breakFlashDuration = 0.15f;

    private Coroutine _breakFlashCoroutine;

    private void Awake()
    {
        ValidateReferences();
        SetBreakFlashAlpha(0f);
    }

    private void OnDisable()
    {
        StopBreakFlash();
        SetBreakFlashAlpha(0f);
    }

    public void SetGauge(float currentGauge, float maxGauge)
    {
        if (_fillImage == null)
        {
            return;
        }

        _fillImage.fillAmount = maxGauge > 0f
            ? Mathf.Clamp01(currentGauge / maxGauge)
            : 0f;
    }

    public void PlayBreakFeedback()
    {
        if (_breakFlashImage == null)
        {
            return;
        }

        StopBreakFlash();
        _breakFlashCoroutine = StartCoroutine(AnimateBreakFlash());
    }

    private IEnumerator AnimateBreakFlash()
    {
        float elapsed = 0f;
        SetBreakFlashAlpha(1f);

        while (elapsed < _breakFlashDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float ratio = Mathf.Clamp01(elapsed / _breakFlashDuration);
            SetBreakFlashAlpha(1f - ratio);
            yield return null;
        }

        SetBreakFlashAlpha(0f);
        _breakFlashCoroutine = null;
    }

    private void StopBreakFlash()
    {
        if (_breakFlashCoroutine == null)
        {
            return;
        }

        StopCoroutine(_breakFlashCoroutine);
        _breakFlashCoroutine = null;
    }

    private void SetBreakFlashAlpha(float alpha)
    {
        if (_breakFlashImage == null)
        {
            return;
        }

        Color color = _breakFlashImage.color;
        color.a = alpha;
        _breakFlashImage.color = color;
    }

    private void ValidateReferences()
    {
        if (_fillImage == null)
        {
            Debug.LogWarning($"[{nameof(UI_GuardGauge)}] Fill Image 참조가 필요합니다.", this);
        }

        if (_breakFlashImage == null)
        {
            Debug.LogWarning($"[{nameof(UI_GuardGauge)}] Break Flash Image 참조가 필요합니다.", this);
        }
    }
}
