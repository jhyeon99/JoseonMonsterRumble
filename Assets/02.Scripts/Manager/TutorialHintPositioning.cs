using UnityEngine;

// Screen-space hints follow the rendered player and final camera pose, then align to display pixels.
internal static class TutorialHintPositioning
{
    internal static Vector3 GetRenderedPlayerAnchor(Player player, bool aboveHead)
    {
        Collider2D body = player.GetComponent<Collider2D>();
        if (body == null) return player.transform.position + (aboveHead ? Vector3.up * 1.5f : Vector3.zero);
        Bounds bounds = body.bounds;
        var anchor = new Vector3(bounds.center.x, aboveHead ? bounds.max.y : bounds.center.y,
            player.transform.position.z);
        Rigidbody2D rigidbody = body.attachedRigidbody;
        if (rigidbody != null && rigidbody.interpolation != RigidbodyInterpolation2D.None)
            anchor += (Vector3)((Vector2)rigidbody.transform.position - rigidbody.position);
        return anchor;
    }

    internal static void Position(RectTransform hint, Camera view, Vector3 worldAnchor, Vector2 offset,
        bool clampToParent = false)
    {
        var parent = hint.parent as RectTransform;
        if (parent == null || view == null) return;
        Canvas canvas = hint.GetComponentInParent<Canvas>();
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        Vector2 screen = (Vector2)view.WorldToScreenPoint(worldAnchor) + offset * scale;
        screen = new Vector2(Mathf.Round(screen.x), Mathf.Round(screen.y));
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out Vector2 position)) return;
        if (clampToParent)
        {
            Vector2 halfSize = hint.rect.size * 0.5f;
            position.x = Mathf.Clamp(position.x, parent.rect.xMin + halfSize.x, parent.rect.xMax - halfSize.x);
            position.y = Mathf.Clamp(position.y, parent.rect.yMin + halfSize.y, parent.rect.yMax - halfSize.y);
            // Clamping to a scaled canvas edge must not reintroduce fractional display pixels.
            Vector2 pixel = RectTransformUtility.WorldToScreenPoint(null, parent.TransformPoint(position));
            pixel = new Vector2(Mathf.Round(pixel.x), Mathf.Round(pixel.y));
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, pixel, null, out position)) return;
        }
        hint.anchoredPosition = position;
    }
}
