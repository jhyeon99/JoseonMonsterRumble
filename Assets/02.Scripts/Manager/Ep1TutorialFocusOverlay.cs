using System;
using UnityEngine;
using UnityEngine.UI;

// Rectangular openings preserve the real HUD and the real death drops in the world.
internal sealed class Ep1TutorialFocusOverlay : IDisposable
{
    private readonly RectTransform _canvas;
    private readonly Image[] _dim = new Image[4];
    private readonly Image[] _border = new Image[4];
    private readonly RectTransform _card;
    private readonly Text _message;
    private readonly Text _continue;
    private readonly Image _enterKey;
    private readonly Vector3[] _corners = new Vector3[4];

    internal Ep1TutorialFocusOverlay(Transform parent)
    {
        var root = new GameObject("Ep1TutorialFocusUI", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(parent, false);
        _canvas = root.GetComponent<RectTransform>();
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32766;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Image blocker = AddImage("BlockHudClicks", _canvas, Color.clear);
        blocker.raycastTarget = true;
        blocker.rectTransform.anchorMax = Vector2.one;
        blocker.rectTransform.sizeDelta = Vector2.zero;
        for (int i = 0; i < _dim.Length; i++) _dim[i] = AddImage("Dim" + i, _canvas, Color.black);
        for (int i = 0; i < _border.Length; i++) _border[i] = AddImage("FocusBorder" + i, _canvas, Color.white);
        _card = AddImage("ExplanationCard", _canvas, new Color(0.025f, 0.03f, 0.04f, 0.92f)).rectTransform;
        _message = AddText("Explanation", _card, TextAnchor.UpperLeft);
        _enterKey = AddImage("EnterKey", _card, Color.white);
        _enterKey.preserveAspect = true;
        _continue = AddText("Continue", _card, TextAnchor.MiddleLeft);
        Hide();
    }

    internal void Show(RectTransform target, SoulItem[] worldSouls, Camera worldCamera, string message, string continueMessage,
        Sprite enterSprite, Font font, int fontSize, FontStyle fontStyle, Color textColor,
        Color outlineColor, float outlineSize, float opacity, Color borderColor, float padding,
        Vector2 textSize, float soulPadding, Vector2 soulMinimumSize, SpriteRenderer worldTarget = null)
    {
        _canvas.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        Vector2 screen = _canvas.rect.size;
        Rect focus = GetFocusRect(target, screen, padding);
        bool hasFocus = target != null;
        if (worldSouls != null)
            hasFocus = TryGetWorldFocusRect(worldSouls, worldCamera, screen, soulPadding, soulMinimumSize, out focus);
        if (worldTarget != null)
            hasFocus = TryGetRendererFocusRect(worldTarget, worldCamera, screen, soulPadding, soulMinimumSize, out focus);
        if (!hasFocus)
        {
            SetRect(_dim[0].rectTransform, new Rect(Vector2.zero, screen));
            for (int i = 1; i < _dim.Length; i++) SetRect(_dim[i].rectTransform, Rect.zero);
        }
        else
        {
            SetRect(_dim[0].rectTransform, new Rect(0f, 0f, screen.x, focus.yMin));
            SetRect(_dim[1].rectTransform, new Rect(0f, focus.yMax, screen.x, screen.y - focus.yMax));
            SetRect(_dim[2].rectTransform, new Rect(0f, focus.yMin, focus.xMin, focus.height));
            SetRect(_dim[3].rectTransform, new Rect(focus.xMax, focus.yMin, screen.x - focus.xMax, focus.height));
        }
        foreach (Image dim in _dim) dim.color = new Color(0f, 0f, 0f, opacity);
        SetBorder(focus, borderColor, hasFocus);

        ApplyStyle(_message, font, fontSize, fontStyle, textColor, outlineColor, outlineSize);
        ApplyStyle(_continue, font, Mathf.Max(18, fontSize - 4), fontStyle, textColor, outlineColor, outlineSize);
        _message.text = message;
        _continue.text = continueMessage;
        float width = Mathf.Min(textSize.x, screen.x - 64f);
        SetRect(_message.rectTransform, new Rect(24f, 0f, width - 48f, textSize.y));
        float height = Mathf.Max(textSize.y, _message.preferredHeight);
        float cardHeight = height + 108f;
        float x = focus.xMax + 28f;
        float y = focus.yMax - cardHeight;
        if (!hasFocus)
        {
            x = (screen.x - width) * 0.5f;
            y = (screen.y - cardHeight) * 0.5f;
        }
        else if (x + width > screen.x - 24f)
        {
            if (focus.xMin - width - 28f >= 24f)
            {
                x = focus.xMin - width - 28f;
            }
            else
            {
                x = Mathf.Clamp(focus.center.x - width * 0.5f, 24f, Mathf.Max(24f, screen.x - width - 24f));
                y = focus.yMin - cardHeight - 24f;
                if (y < 24f) y = focus.yMax + 24f;
            }
        }
        x = Mathf.Clamp(x, 24f, Mathf.Max(24f, screen.x - width - 24f));
        y = Mathf.Clamp(y, 24f, Mathf.Max(24f, screen.y - cardHeight - 24f));
        SetRect(_card, new Rect(x, y, width, cardHeight));
        SetRect(_message.rectTransform, new Rect(24f, 84f, width - 48f, height));
        _enterKey.sprite = enterSprite;
        _enterKey.gameObject.SetActive(enterSprite != null);
        SetRect(_enterKey.rectTransform, new Rect(24f, 18f, 94f, 52f));
        float continueX = enterSprite != null ? 132f : 24f;
        SetRect(_continue.rectTransform, new Rect(continueX, 18f, width - continueX - 24f, 52f));
    }

    private Rect GetFocusRect(RectTransform target, Vector2 screen, float padding)
    {
        if (target == null) return new Rect(screen * 0.5f, Vector2.zero);
        Canvas source = target.GetComponentInParent<Canvas>();
        Camera camera = source != null && source.renderMode != RenderMode.ScreenSpaceOverlay ? source.worldCamera : null;
        target.GetWorldCorners(_corners);
        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (Vector3 corner in _corners)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, point, null, out Vector2 local);
            local += screen * 0.5f;
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }
        min = Vector2.Max(Vector2.zero, min - Vector2.one * padding);
        max = Vector2.Min(screen, max + Vector2.one * padding);
        return Rect.MinMaxRect(min.x, min.y, Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
    }

    internal static bool AreSoulsVisible(SoulItem[] souls, Camera camera)
    {
        if (souls == null || camera == null) return false;
        foreach (SoulItem soul in souls)
        {
            if (TryGetViewportBounds(soul, camera, out Rect bounds) &&
                bounds.Overlaps(new Rect(0f, 0f, 1f, 1f)))
            {
                return true;
            }
        }
        return false;
    }

    private bool TryGetWorldFocusRect(SoulItem[] souls, Camera camera, Vector2 screen,
        float padding, Vector2 minimumSize, out Rect focus)
    {
        focus = new Rect(screen * 0.5f, Vector2.zero);
        if (camera == null) return false;
        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        bool found = false;
        foreach (SoulItem soul in souls)
        {
            if (!TryGetViewportBounds(soul, camera, out Rect viewport) ||
                !viewport.Overlaps(new Rect(0f, 0f, 1f, 1f)))
            {
                continue;
            }

            Vector2 pixelMin = camera.pixelRect.position + Vector2.Scale(viewport.min, camera.pixelRect.size);
            Vector2 pixelMax = camera.pixelRect.position + Vector2.Scale(viewport.max, camera.pixelRect.size);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, pixelMin, null, out Vector2 localMin);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, pixelMax, null, out Vector2 localMax);
            min = Vector2.Min(min, localMin + screen * 0.5f);
            max = Vector2.Max(max, localMax + screen * 0.5f);
            found = true;
        }
        if (!found) return false;
        Vector2 center = (min + max) * 0.5f;
        var size = Vector2.Min(screen, Vector2.Max(minimumSize, max - min + Vector2.one * padding * 2f));
        Vector2 position = center - size * 0.5f;
        position.x = Mathf.Clamp(position.x, 0f, screen.x - size.x);
        position.y = Mathf.Clamp(position.y, 0f, screen.y - size.y);
        focus = new Rect(position, size);
        return true;
    }

    private static bool TryGetViewportBounds(SoulItem soul, Camera camera, out Rect bounds)
    {
        bounds = Rect.zero;
        if (soul == null || !soul.gameObject.activeInHierarchy) return false;
        SpriteRenderer sprite = soul.GetComponentInChildren<SpriteRenderer>();
        return TryGetSpriteViewportBounds(sprite, camera, out bounds);
    }

    private bool TryGetRendererFocusRect(SpriteRenderer sprite, Camera camera, Vector2 screen,
        float padding, Vector2 minimumSize, out Rect focus)
    {
        focus = Rect.zero;
        if (camera == null || !TryGetSpriteViewportBounds(sprite, camera, out Rect viewport)) return false;
        Vector2 pixelMin = camera.pixelRect.position + Vector2.Scale(viewport.min, camera.pixelRect.size);
        Vector2 pixelMax = camera.pixelRect.position + Vector2.Scale(viewport.max, camera.pixelRect.size);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, pixelMin, null, out Vector2 localMin);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, pixelMax, null, out Vector2 localMax);
        var size = Vector2.Min(screen, Vector2.Max(minimumSize, localMax - localMin + Vector2.one * padding * 2f));
        Vector2 center = (localMin + localMax) * 0.5f + screen * 0.5f;
        Vector2 position = center - size * 0.5f;
        position.x = Mathf.Clamp(position.x, 0f, screen.x - size.x);
        position.y = Mathf.Clamp(position.y, 0f, screen.y - size.y);
        focus = new Rect(position, size);
        return true;
    }

    private static bool TryGetSpriteViewportBounds(SpriteRenderer sprite, Camera camera, out Rect bounds)
    {
        bounds = Rect.zero;
        if (sprite == null || !sprite.gameObject.activeInHierarchy || !sprite.enabled || sprite.sprite == null) return false;
        Bounds world = sprite.bounds;
        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int x = 0; x < 2; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                Vector3 point = camera.WorldToViewportPoint(new Vector3(x == 0 ? world.min.x : world.max.x,
                    y == 0 ? world.min.y : world.max.y, world.center.z));
                if (point.z <= 0f) return false;
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
        }
        bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return true;
    }

    private void SetBorder(Rect rect, Color color, bool visible)
    {
        foreach (Image image in _border)
        {
            image.gameObject.SetActive(visible);
            image.color = color;
        }
        const float thickness = 2f;
        SetRect(_border[0].rectTransform, new Rect(rect.xMin, rect.yMin, rect.width, thickness));
        SetRect(_border[1].rectTransform, new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness));
        SetRect(_border[2].rectTransform, new Rect(rect.xMin, rect.yMin, thickness, rect.height));
        SetRect(_border[3].rectTransform, new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height));
    }

    private static Image AddImage(string name, Transform parent, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Text AddText(string name, Transform parent, TextAnchor alignment)
    {
        Text text = CreateRect(name, parent).gameObject.AddComponent<Text>();
        text.alignment = alignment;
        text.raycastTarget = false;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        return text;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        return rect;
    }

    private static void SetRect(RectTransform rect, Rect bounds)
    {
        rect.anchoredPosition = bounds.position;
        rect.sizeDelta = bounds.size;
    }

    private static void ApplyStyle(Text text, Font font, int size, FontStyle style, Color color,
        Color outlineColor, float outlineSize)
    {
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        Outline outline = text.GetComponent<Outline>();
        if (outline == null) outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(outlineSize, -outlineSize);
        outline.enabled = outlineSize > 0f;
    }

    internal void Hide() => _canvas.gameObject.SetActive(false);

    public void Dispose()
    {
        if (_canvas != null) UnityEngine.Object.Destroy(_canvas.gameObject);
    }
}
