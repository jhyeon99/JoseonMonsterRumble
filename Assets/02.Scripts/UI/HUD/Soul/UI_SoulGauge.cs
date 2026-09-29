using System.Collections;
using UnityEngine;

public class UI_SoulGauge : MonoBehaviour
{
    [Header("공통 설정")]
    [SerializeField] private UI_SoulOrbSettings _orbSettings;

    [Header("게이지 전환")]
    [SerializeField, Min(0f)] private float _fillDuration = 0.2f;
    [SerializeField] private AnimationCurve _fillCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("오브 슬롯")]
    [SerializeField] private UI_SoulOrbSlot[] _orbSlots;

    private Coroutine _fillCoroutine;
    private float _displayedRatio;

    private void Awake()
    {
        InitializeOrbSlots();
        WarnAboutInvalidReferences();
        ApplyRatioImmediately(0f);
    }

    private void OnDisable()
    {
        StopFillCoroutine();
    }

    private void OnValidate()
    {
        _fillDuration = Mathf.Max(0f, _fillDuration);
        ConfigureOrbSlots();
    }

    public void SetSoul(float currentSoul, float maxSoul)
    {
        if (maxSoul <= 0f)
        {
            Debug.LogWarning($"[{nameof(UI_SoulGauge)}] 최대 영혼값은 0보다 커야 합니다.", this);
            SetTargetRatio(0f);
            return;
        }

        SetTargetRatio(Mathf.Clamp01(currentSoul / maxSoul));
    }

    public void SetSoulImmediately(float currentSoul, float maxSoul)
    {
        if (maxSoul <= 0f)
        {
            Debug.LogWarning($"[{nameof(UI_SoulGauge)}] 최대 영혼값은 0보다 커야 합니다.", this);
            SetSoulRatioImmediately(0f);
            return;
        }

        SetSoulRatioImmediately(currentSoul / maxSoul);
    }

    public void SetSoulRatio(float ratio)
    {
        SetTargetRatio(Mathf.Clamp01(ratio));
    }

    public void SetSoulRatioImmediately(float ratio)
    {
        StopFillCoroutine();
        ApplyRatioImmediately(ratio);
    }

    public int GetOrbCount()
    {
        return _orbSlots == null ? 0 : _orbSlots.Length;
    }

    private void ConfigureOrbSlots()
    {
        if (_orbSlots == null || _orbSettings == null)
        {
            return;
        }

        foreach (UI_SoulOrbSlot orbSlot in _orbSlots)
        {
            if (orbSlot != null)
            {
                orbSlot.Configure(_orbSettings);
            }
        }
    }

    private void InitializeOrbSlots()
    {
        if (_orbSlots == null || _orbSettings == null)
        {
            return;
        }

        foreach (UI_SoulOrbSlot orbSlot in _orbSlots)
        {
            if (orbSlot != null)
            {
                orbSlot.Initialize(_orbSettings);
            }
        }
    }

    private void SetTargetRatio(float targetRatio)
    {
        StopFillCoroutine();

        if (!isActiveAndEnabled || _fillDuration <= 0f)
        {
            ApplyRatioImmediately(targetRatio);
            return;
        }

        _fillCoroutine = StartCoroutine(AnimateFill(targetRatio));
    }

    private IEnumerator AnimateFill(float targetRatio)
    {
        float startRatio = _displayedRatio;
        float elapsed = 0f;

        while (elapsed < _fillDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / _fillDuration);
            float curvedProgress = _fillCurve == null ? progress : _fillCurve.Evaluate(progress);
            ApplyRatio(Mathf.Lerp(startRatio, targetRatio, curvedProgress), true);
            yield return null;
        }

        ApplyRatioImmediately(targetRatio);
        _fillCoroutine = null;
    }

    private void ApplyRatioImmediately(float ratio)
    {
        ApplyRatio(Mathf.Clamp01(ratio), false);
    }

    private void ApplyRatio(float ratio, bool playFeedback)
    {
        _displayedRatio = ratio;

        int orbCount = GetOrbCount();
        if (orbCount == 0)
        {
            return;
        }

        float totalFill = ratio * orbCount;

        for (int index = 0; index < orbCount; index++)
        {
            UI_SoulOrbSlot orbSlot = _orbSlots[index];
            if (orbSlot != null)
            {
                orbSlot.SetFillAmount(Mathf.Clamp01(totalFill - index), playFeedback);
            }
        }
    }

    private void WarnAboutInvalidReferences()
    {
        if (_orbSettings == null)
        {
            Debug.LogWarning($"[{nameof(UI_SoulGauge)}] Soul Orb Settings 참조가 필요합니다.", this);
        }
        else if (!_orbSettings.HasRequiredReferences())
        {
            Debug.LogWarning($"[{nameof(UI_SoulGauge)}] Soul Orb Settings의 필수 리소스가 없습니다.", this);
        }

        if (_orbSlots == null || _orbSlots.Length == 0)
        {
            Debug.LogWarning($"[{nameof(UI_SoulGauge)}] Orb Slot이 하나 이상 필요합니다.", this);
            return;
        }

        for (int index = 0; index < _orbSlots.Length; index++)
        {
            if (_orbSlots[index] == null)
            {
                Debug.LogWarning($"[{nameof(UI_SoulGauge)}] Orb Slot {index + 1} 참조가 없습니다.", this);
            }
        }
    }

    private void StopFillCoroutine()
    {
        if (_fillCoroutine == null)
        {
            return;
        }

        StopCoroutine(_fillCoroutine);
        _fillCoroutine = null;
    }
}
