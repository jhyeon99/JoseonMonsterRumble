using Com.LuisPedroFonseca.ProCamera2D;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(100)]
public sealed class ProCamera2DLetterboxOverlay : MonoBehaviour
{
    [SerializeField] private ProCamera2DCinematics _cinematics;
    private ProCamera2DLetterbox _source;
    private GameObject _root;
    private Image _top;
    private Image _bottom;

    private void Awake()
    {
        if (_cinematics == null) _cinematics = GetComponent<ProCamera2DCinematics>();
        var canvasObject = new GameObject("CinematicLetterboxOverlay", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);
        _root = canvasObject;
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Dialogue and key guides stay readable above the cinematic bars.
        canvas.sortingOrder = 32764;
        _top = CreateBar("Top", canvasObject.transform);
        _bottom = CreateBar("Bottom", canvasObject.transform);
        ApplyAmount(0f, Color.black);
    }

    private Image CreateBar(string name, Transform parent)
    {
        var bar = new GameObject(name, typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(parent, false);
        Image image = bar.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private void LateUpdate()
    {
        if (_cinematics == null) return;
        if (_source == null) _source = _cinematics.GetComponentInChildren<ProCamera2DLetterbox>();
        // Cinematics owns the tween. Only rendering is replaced for URP compatibility.
        float amount = _source != null && _cinematics.UseLetterbox ? _source.Amount : 0f;
        ApplyAmount(Mathf.Clamp(amount, 0f, 0.5f), _cinematics.LetterboxColor);
    }

    private void ApplyAmount(float amount, Color color)
    {
        RectTransform top = _top.rectTransform;
        top.anchorMin = new Vector2(0f, 1f - amount);
        top.anchorMax = Vector2.one;
        top.offsetMin = Vector2.zero;
        top.offsetMax = Vector2.zero;
        RectTransform bottom = _bottom.rectTransform;
        bottom.anchorMin = Vector2.zero;
        bottom.anchorMax = new Vector2(1f, amount);
        bottom.offsetMin = Vector2.zero;
        bottom.offsetMax = Vector2.zero;
        _top.color = color;
        _bottom.color = color;
        _top.enabled = amount > 0f;
        _bottom.enabled = amount > 0f;
    }

    private void OnEnable()
    {
        if (_root != null) _root.SetActive(true);
    }

    private void OnDisable()
    {
        if (_root != null) _root.SetActive(false);
    }
}
