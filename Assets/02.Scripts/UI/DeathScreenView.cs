using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public sealed class DeathScreenView : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _fadeInDuration = 0.35f;
    [SerializeField, Min(0f)] private float _fadeOutDuration = 0.5f;

    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        HideImmediate();
    }

    public IEnumerator FadeIn()
    {
        _canvasGroup.blocksRaycasts = true;
        yield return FadeTo(1f, _fadeInDuration);
    }

    public IEnumerator FadeOut()
    {
        yield return FadeTo(0f, _fadeOutDuration);
        HideImmediate();
    }

    public void HideImmediate()
    {
        if (_canvasGroup == null)
        {
            return;
        }

        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        if (duration <= 0f)
        {
            _canvasGroup.alpha = targetAlpha;
            yield break;
        }

        float startAlpha = _canvasGroup.alpha;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Lerp(
                startAlpha,
                targetAlpha,
                elapsedTime / duration
            );
            yield return null;
        }

        _canvasGroup.alpha = targetAlpha;
    }
}
