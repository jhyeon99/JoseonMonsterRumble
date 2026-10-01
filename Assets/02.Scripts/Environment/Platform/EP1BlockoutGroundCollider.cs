using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(PolygonCollider2D))]
public sealed class EP1BlockoutGroundCollider : MonoBehaviour
{
    // Unity grid screenshot: approximately 173 screen pixels per 1 world unit.
    // The Sprite pivot is the left end of the first walkable surface.
    private static readonly Vector2[] Profile =
    {
        new Vector2(0.00f,  0.00f),
        new Vector2(4.03f,  0.00f),
        new Vector2(4.03f,  0.20f),
        new Vector2(6.03f,  0.20f),
        new Vector2(6.03f,  0.00f),
        new Vector2(6.43f,  0.00f),
        new Vector2(6.43f,  0.10f),
        new Vector2(7.29f,  0.10f),
        new Vector2(7.29f,  0.03f),
        new Vector2(9.57f,  0.03f),
        new Vector2(9.57f, -0.70f),
        new Vector2(0.00f, -0.70f)
    };

    private void Reset()
    {
        ApplyShape();
    }

    private void Awake()
    {
        ApplyShape();
    }

    [ContextMenu("Apply Blockout Collider")]
    public void ApplyShape()
    {
        PolygonCollider2D ground = GetComponent<PolygonCollider2D>();
        ground.pathCount = 1;
        ground.SetPath(0, Profile);
    }
}
