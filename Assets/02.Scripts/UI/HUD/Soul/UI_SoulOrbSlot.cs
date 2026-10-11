using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UI_SoulOrbSlot : MonoBehaviour
{
    private enum OrbState
    {
        Uninitialized,
        Empty,
        Partial,
        Full,
        Consuming
    }

    [SerializeField, HideInInspector] private Image _emptyImage;
    [SerializeField, HideInInspector] private Image _fillImage;
    [SerializeField, HideInInspector] private Image _fullChargeImage;
    [SerializeField, HideInInspector] private Animator _fullChargeAnimator;

    private UI_SoulOrbSettings _settings;
    private Coroutine _feedbackCoroutine;
    private OrbState _state = OrbState.Uninitialized;
    private OrbState _targetState = OrbState.Empty;

    private void OnDisable()
    {
        StopFeedbackCoroutine();

        if (_state == OrbState.Consuming)
        {
            _state = _targetState;
        }

        ApplyStableVisualState(_state, false);
    }

    public void Configure(UI_SoulOrbSettings settings)
    {
        _settings = settings;

        if (_settings == null)
        {
            return;
        }

        ApplySprite(_emptyImage, _settings.EmptySprite);
        ApplySprite(_fillImage, _settings.FilledSprite);
        ConfigureFillImage();
        ApplySlotScale(1f);
        ApplyAnimatedImageScale(_settings.FullChargeScale);
        ConfigureAnimator();

        if (Application.isPlaying)
        {
            WarnAboutInvalidReferences();
        }
    }

    public void Initialize(UI_SoulOrbSettings settings)
    {
        StopFeedbackCoroutine();
        _state = OrbState.Uninitialized;
        _targetState = OrbState.Empty;
        Configure(settings);
    }

    public void SetFillAmount(float fillAmount, bool playFeedback)
    {
        float normalizedFill = Mathf.Clamp01(fillAmount);
        if (_fillImage != null)
        {
            _fillImage.fillAmount = CalculateImageFillAmount(normalizedFill);
        }

        OrbState nextState = CalculateStableState(normalizedFill);
        TransitionTo(nextState, playFeedback);
    }

    private float CalculateImageFillAmount(float normalizedFill)
    {
        if (normalizedFill <= 0f || normalizedFill >= 1f)
        {
            return normalizedFill;
        }

        Vector2 heightRange = _settings.FillVisibleHeightRange;
        return Mathf.Clamp01(Mathf.Lerp(heightRange.x, heightRange.y, normalizedFill));
    }

    private OrbState CalculateStableState(float fillAmount)
    {
        if (Mathf.Approximately(fillAmount, 1f))
        {
            return OrbState.Full;
        }

        return fillAmount > 0f ? OrbState.Partial : OrbState.Empty;
    }

    private void TransitionTo(OrbState nextState, bool playFeedback)
    {
        if (_state == OrbState.Uninitialized)
        {
            SetStableState(nextState, false);
            return;
        }

        if (_state == OrbState.Consuming)
        {
            _targetState = nextState;

            if (nextState == OrbState.Full)
            {
                SetStableState(OrbState.Full, playFeedback);
            }

            return;
        }

        if (_state == nextState)
        {
            return;
        }

        if (_state == OrbState.Full && nextState != OrbState.Full && playFeedback && Application.isPlaying)
        {
            StartConsumeFeedback(nextState);
            return;
        }

        SetStableState(nextState, nextState == OrbState.Full && playFeedback && Application.isPlaying);
    }

    private void SetStableState(OrbState stableState, bool playGainFeedback)
    {
        StopFeedbackCoroutine();
        _state = stableState;
        _targetState = stableState;
        ApplyStableVisualState(stableState, true);

        if (stableState == OrbState.Full && playGainFeedback)
        {
            _feedbackCoroutine = StartCoroutine(AnimateGainFeedback());
        }
    }

    private void StartConsumeFeedback(OrbState targetState)
    {
        StopFeedbackCoroutine();
        _state = OrbState.Consuming;
        _targetState = targetState;

        if (_fullChargeImage == null || _fullChargeAnimator == null)
        {
            Debug.LogWarning($"[{nameof(UI_SoulOrbSlot)}] 소비 Image와 Animator 참조가 필요합니다.", this);
            SetStableState(targetState, false);
            return;
        }

        EnableConsumeImage();
        ApplyAnimatedImageScale(_settings.ConsumeScale);

        if (!PlayAnimatorState(_settings.ConsumeStateHash))
        {
            SetStableState(targetState, false);
            return;
        }

        _feedbackCoroutine = StartCoroutine(AnimateConsumeFeedback());
    }

    private IEnumerator AnimateGainFeedback()
    {
        float duration = _settings.GainDuration;
        if (duration <= 0f)
        {
            CompleteGainFeedback();
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            ApplySlotScale(_settings.CalculateGainScale(progress));
            ApplyFlash(_settings.CalculateGainFlash(progress));
            yield return null;
        }

        CompleteGainFeedback();
    }

    private IEnumerator AnimateConsumeFeedback()
    {
        float duration = _settings.ConsumeDuration;

        if (duration <= 0f)
        {
            CompleteConsumeFeedback();
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            ApplySlotScale(_settings.CalculateConsumeScale(progress));

            _fullChargeImage.color = Color.Lerp(Color.white, _settings.FlashColor, _settings.CalculateConsumeFlash(progress));

            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }

        CompleteConsumeFeedback();
    }

    private void CompleteGainFeedback()
    {
        _feedbackCoroutine = null;
        ResetFeedbackVisual();
    }

    private void CompleteConsumeFeedback()
    {
        _feedbackCoroutine = null;
        DisableAnimatedImage();
        ResetFeedbackVisual();
        SetStableState(_targetState, false);
    }

    private void ApplyStableVisualState(OrbState stableState, bool restartAnimation)
    {
        ResetFeedbackVisual();

        if (stableState == OrbState.Full)
        {
            EnableAnimatedImage();
            ApplyAnimatedImageScale(_settings.FullChargeScale);

            if (restartAnimation && Application.isPlaying)
            {
                PlayAnimatorState(_settings.FullChargeStateHash);
            }

            return;
        }

        DisableAnimatedImage();
    }

    private void EnableAnimatedImage()
    {
        if (_fullChargeImage != null)
        {
            _fullChargeImage.enabled = true;
            _fullChargeImage.color = Color.white;
        }

        if (_fullChargeAnimator != null)
        {
            _fullChargeAnimator.enabled = Application.isPlaying;
        }
    }

    private void EnableConsumeImage()
    {
        _fullChargeImage.enabled = true;
        _fullChargeImage.color = Color.white;
        _fullChargeAnimator.speed = 1f;
        _fullChargeAnimator.enabled = true;
    }

    private void DisableAnimatedImage()
    {
        if (_fullChargeImage != null)
        {
            _fullChargeImage.enabled = false;
            _fullChargeImage.color = Color.white;
        }

        if (_fullChargeAnimator != null)
        {
            _fullChargeAnimator.speed = 1f;
            _fullChargeAnimator.enabled = false;
        }
    }

    private void ConfigureAnimator()
    {
        if (_fullChargeImage != null && _settings.FullChargePreviewSprite != null)
        {
            _fullChargeImage.sprite = _settings.FullChargePreviewSprite;
        }

        if (_fullChargeAnimator != null)
        {
            _fullChargeAnimator.runtimeAnimatorController = _settings.AnimatorController;
        }
    }

    private bool PlayAnimatorState(int stateHash)
    {
        if (!Application.isPlaying || _fullChargeAnimator == null || _settings.AnimatorController == null)
        {
            return false;
        }

        _fullChargeAnimator.enabled = true;
        _fullChargeAnimator.Rebind();
        _fullChargeAnimator.Update(0f);

        if (!_fullChargeAnimator.HasState(0, stateHash))
        {
            Debug.LogWarning($"[{nameof(UI_SoulOrbSlot)}] Animator 상태를 찾을 수 없습니다.", this);
            return false;
        }

        _fullChargeAnimator.Play(stateHash, 0, 0f);
        _fullChargeAnimator.Update(0f);

        return true;
    }

    private void ApplySprite(Image image, Sprite sprite)
    {
        if (image != null && sprite != null)
        {
            image.sprite = sprite;
        }
    }

    private void ConfigureFillImage()
    {
        if (_fillImage == null)
        {
            return;
        }

        _fillImage.type = Image.Type.Filled;
        _fillImage.fillMethod = Image.FillMethod.Vertical;
        _fillImage.fillOrigin = (int)Image.OriginVertical.Bottom;
        _fillImage.rectTransform.localScale = Vector3.one;
    }

    private void ApplySlotScale(float scaleMultiplier)
    {
        if (_emptyImage != null)
        {
            _emptyImage.rectTransform.localScale = _settings.BaseOrbScale * scaleMultiplier;
        }
    }

    private void ApplyAnimatedImageScale(Vector3 targetScale)
    {
        if (_fullChargeImage != null)
        {
            _fullChargeImage.rectTransform.localScale = CalculateChildScale(targetScale, _settings.BaseOrbScale);
        }
    }

    private void ApplyFlash(float intensity)
    {
        if (_fullChargeImage != null)
        {
            _fullChargeImage.color = Color.Lerp(Color.white, _settings.FlashColor, Mathf.Clamp01(intensity));
        }
    }

    private Vector3 CalculateChildScale(Vector3 targetScale, Vector3 parentScale)
    {
        return new Vector3(
            CalculateScaleAxis(targetScale.x, parentScale.x),
            CalculateScaleAxis(targetScale.y, parentScale.y),
            CalculateScaleAxis(targetScale.z, parentScale.z)
        );
    }

    private float CalculateScaleAxis(float targetScale, float parentScale)
    {
        return Mathf.Approximately(parentScale, 0f) ? targetScale : targetScale / parentScale;
    }

    private void StopFeedbackCoroutine()
    {
        if (_feedbackCoroutine == null)
        {
            return;
        }

        StopCoroutine(_feedbackCoroutine);
        _feedbackCoroutine = null;
        ResetFeedbackVisual();
    }

    private void ResetFeedbackVisual()
    {
        if (_settings == null)
        {
            return;
        }

        ApplySlotScale(1f);
        ApplyFlash(0f);

        if (_state != OrbState.Consuming)
        {
            ApplyAnimatedImageScale(_settings.FullChargeScale);
        }
    }

    private void WarnAboutInvalidReferences()
    {
        if (_emptyImage == null || _fillImage == null || _fullChargeImage == null || _fullChargeAnimator == null)
        {
            Debug.LogWarning($"[{nameof(UI_SoulOrbSlot)}] 오브 표시 참조가 모두 필요합니다.", this);
        }
    }
}
