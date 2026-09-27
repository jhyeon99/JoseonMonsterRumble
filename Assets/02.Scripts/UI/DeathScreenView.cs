using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public sealed class DeathScreenView : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _fadeInDuration = 0.35f;

    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        HideImmediate();
    }

    public IEnumerator FadeIn()
    {
        _canvasGroup.blocksRaycasts = true;
        yield return FadeTo(1f);
    }

    public IEnumerator FadeOut()
    {
        HideImmediate();
        yield break;
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

    private IEnumerator FadeTo(float targetAlpha)
    {
        if (_fadeInDuration <= 0f)
        {
            _canvasGroup.alpha = targetAlpha;
            yield break;
        }

        float startAlpha = _canvasGroup.alpha;
        float elapsedTime = 0f;

        while (elapsedTime < _fadeInDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Lerp(
                startAlpha,
                targetAlpha,
                elapsedTime / _fadeInDuration
            );
            yield return null;
        }

        _canvasGroup.alpha = targetAlpha;
    }
}
