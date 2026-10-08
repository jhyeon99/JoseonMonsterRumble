using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SceneArrivalFade : MonoBehaviour
{
    private static SceneArrivalFade _instance;
    private CanvasGroup _overlay;
    private float _duration;

    public static void Prepare(float duration)
    {
        if (_instance == null)
        {
            var overlayObject = new GameObject("SceneArrivalFade", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            overlayObject.AddComponent<SceneArrivalFade>();
        }
        _instance._duration = Mathf.Max(0.01f, duration);
        _instance._overlay.alpha = 1f;
    }

    private void Awake()
    {
        _instance = this;
        DontDestroyOnLoad(gameObject);
        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;
        _overlay = GetComponent<CanvasGroup>();
        _overlay.alpha = 1f;
        _overlay.blocksRaycasts = true;
        gameObject.AddComponent<GraphicRaycaster>();
        var screen = new GameObject("BlackScreen", typeof(RectTransform), typeof(Image));
        screen.transform.SetParent(transform, false);
        RectTransform rect = screen.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        screen.GetComponent<Image>().color = Color.black;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(Reveal());
    }

    private IEnumerator Reveal()
    {
        // Let the destination player's Start and camera initialization finish under black.
        yield return null;
        float elapsed = 0f;
        while (elapsed < _duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _overlay.alpha = 1f - Mathf.Clamp01(elapsed / _duration);
            yield return null;
        }
        _overlay.alpha = 0f;
        _overlay.blocksRaycasts = false;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (_instance == this) _instance = null;
    }
}
