using UnityEngine;

/// <summary>
/// 패턴 사용 조건용: 플레이어가 판정(BossHitbox) 범위 안에 있는지.
/// 판정 콜라이더는 평소 꺼져 있어 bounds를 쓸 수 없으므로 BoxCollider2D의 크기 / 위치로 직접 계산.
/// </summary>
public static class BossHitboxRange
{
    /// <summary>
    /// 판정 범위(+ 여유) 안에 플레이어 몸이 걸치는지.
    /// mirrorAroundX를 주면 그 x를 기준으로 좌우 반전한 범위로 검사 (보스가 반대쪽을 볼 때의 범위).
    /// 판정에 BoxCollider2D가 없으면 false
    /// </summary>
    public static bool ContainsPlayer(BossHitbox hitbox, Transform player, Vector2 margin, float? mirrorAroundX = null)
    {
        if (!TryGetWorldRect(hitbox, out Rect rect))
        {
            return false;
        }

        if (mirrorAroundX.HasValue)
        {
            float pivot = mirrorAroundX.Value;
            float minX = (2f * pivot) - rect.xMax;
            rect = new Rect(minX, rect.yMin, rect.width, rect.height);
        }

        rect.xMin -= margin.x;
        rect.xMax += margin.x;
        rect.yMin -= margin.y;
        rect.yMax += margin.y;

        if (player == null)
        {
            return false;
        }

        // 플레이어 몸 콜라이더가 있으면 몸이 조금이라도 걸치면 범위 안
        Collider2D body = FindBodyCollider(player);
        if (body != null)
        {
            Bounds b = body.bounds;
            var playerRect = new Rect(b.min.x, b.min.y, b.size.x, b.size.y);
            return rect.Overlaps(playerRect);
        }

        return rect.Contains(player.position);
    }

    /// <summary>판정 BoxCollider2D의 월드 범위 (회전 / 반전 포함한 외곽 사각형)</summary>
    public static bool TryGetWorldRect(BossHitbox hitbox, out Rect rect)
    {
        rect = default;
        if (hitbox == null)
        {
            return false;
        }

        BoxCollider2D box = hitbox.GetComponent<BoxCollider2D>();
        if (box == null)
        {
            return false;
        }

        Transform t = box.transform;
        Vector2 half = box.size * 0.5f;
        Vector2 offset = box.offset;

        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < 4; i++)
        {
            var local = new Vector2(
                offset.x + (((i & 1) == 0) ? -half.x : half.x),
                offset.y + (((i & 2) == 0) ? -half.y : half.y));
            Vector2 world = t.TransformPoint(local);
            min = Vector2.Min(min, world);
            max = Vector2.Max(max, world);
        }

        rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return true;
    }

    private static Collider2D FindBodyCollider(Transform player)
    {
        foreach (Collider2D collider in player.GetComponents<Collider2D>())
        {
            if (collider != null && collider.enabled && !collider.isTrigger)
            {
                return collider;
            }
        }

        return null;
    }

    /// <summary>씬 뷰 확인용: 범위 사각형 그리기</summary>
    public static void DrawGizmo(Rect rect, Color color)
    {
        Gizmos.color = color;
        Gizmos.DrawWireCube(rect.center, rect.size);
    }
}