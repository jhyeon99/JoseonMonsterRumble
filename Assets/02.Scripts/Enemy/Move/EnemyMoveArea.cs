using UnityEngine;

public class EnemyMoveArea : MonoBehaviour
{
    [Header("이동 범위 설정 (중심점 기준)")]

    public Vector2 CenterOffset;
    public Vector2 AreaSize = new Vector2(3f, 3f);

    [Header("영역 색상")]

    public Color AreaColor = new Color(0f, 1f, 0f, 0.3f);

    private Vector2 _startPosition;

    private void Awake()
    {
        _startPosition = transform.position;
    }

    private void Update()
    {
        ClampPosition();
    }

    private void ClampPosition()
    {
        Vector2 center = _startPosition + CenterOffset;
        Vector2 minBounds = center - (AreaSize * 0.5f);
        Vector2 maxBounds = center + (AreaSize * 0.5f);

        float clampedX = Mathf.Clamp(transform.position.x, minBounds.x, maxBounds.x);
        float clampedY = Mathf.Clamp(transform.position.y, minBounds.y, maxBounds.y);

        transform.position = new Vector3(clampedX, clampedY, transform.position.z);
    }

    private void OnDrawGizmos()
    {
        Vector2 basePos = Application.isPlaying ? _startPosition : (Vector2)transform.position;
        Vector2 center = basePos + CenterOffset;

        Gizmos.color = AreaColor;
        Gizmos.DrawCube(new Vector3(center.x, center.y, transform.position.z),
            new Vector3(AreaSize.x, AreaSize.y, 0.1f));

        Gizmos.color = new Color(AreaColor.r, AreaColor.g, AreaColor.b, 1f);
        Gizmos.DrawWireCube(new Vector3(center.x, center.y, transform.position.z),
            new Vector3(AreaSize.x, AreaSize.y, 0.1f));
    }
}
