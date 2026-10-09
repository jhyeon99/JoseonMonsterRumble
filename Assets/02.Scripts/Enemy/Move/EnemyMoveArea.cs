using UnityEngine;

public class EnemyMoveArea : MonoBehaviour
{
    [Header("이동 범위 설정 (중심점 기준)")]

    public Vector2 CenterOffset;
    public Vector2 AreaSize = new Vector2(3f, 3f);

    [SerializeField, Min(0f)] private float _chaseReleaseMargin;

    [Header("영역 색상")]

    public Color AreaColor = new Color(0f, 1f, 0f, 0.3f);

    private Vector2 _startPosition;
    public Vector2 StartPosition => _startPosition;

    private void Awake()
    {
        _startPosition = transform.position;
    }

    public bool IsPositionInArea(Vector2 targetPosition)
    {
        return IsPositionInBounds(targetPosition, StartPosition + CenterOffset, 0f);
    }

    public bool IsPositionDetected(Vector2 targetPosition)
    {
        return IsPositionInBounds(targetPosition, (Vector2)transform.position + CenterOffset, 0f);
    }

    public bool IsPositionInChaseArea(Vector2 targetPosition)
    {
        return IsPositionInBounds(targetPosition, (Vector2)transform.position + CenterOffset, _chaseReleaseMargin);
    }

    private bool IsPositionInBounds(Vector2 targetPosition, Vector2 center, float margin)
    {
        Vector2 halfSize = AreaSize * 0.5f + Vector2.one * margin;

        Vector2 minBounds = center - halfSize;
        Vector2 maxBounds = center + halfSize;

        return targetPosition.x >= minBounds.x && targetPosition.x <= maxBounds.x &&
               targetPosition.y >= minBounds.y && targetPosition.y <= maxBounds.y;
    }

    public Vector2 ClampPosition(Vector2 position)
    {
        Vector2 center = StartPosition + CenterOffset;
        Vector2 halfSize = AreaSize * 0.5f;
        return new Vector2(Mathf.Clamp(position.x, center.x - halfSize.x, center.x + halfSize.x),
            Mathf.Clamp(position.y, center.y - halfSize.y, center.y + halfSize.y));
    }

    public float ClampX(float x)
    {
        Vector2 basePos = Application.isPlaying ? _startPosition : transform.position;
        float center = basePos.x + CenterOffset.x;
        float halfWidth = AreaSize.x * 0.5f;

        float mixWidth = center - halfWidth;
        float maxWidth = center + halfWidth;

        return Mathf.Clamp(x, mixWidth, maxWidth);
    }

    private void OnDrawGizmos()
    {
        Vector2 basePos = Application.isPlaying ? _startPosition : transform.position;
        Vector2 center = basePos + CenterOffset;

        Gizmos.color = AreaColor;
        Gizmos.DrawCube(new Vector3(center.x, center.y, transform.position.z),
            new Vector3(AreaSize.x, AreaSize.y, 0.1f));

        Gizmos.color = new Color(AreaColor.r, AreaColor.g, AreaColor.b, 1f);
        Gizmos.DrawWireCube(new Vector3(center.x, center.y, transform.position.z),
            new Vector3(AreaSize.x, AreaSize.y, 0.1f));

        Gizmos.color = Color.cyan;
        Vector2 detectionCenter = (Vector2)transform.position + CenterOffset;
        var detectionPosition = new Vector3(detectionCenter.x, detectionCenter.y, transform.position.z);
        Gizmos.DrawWireCube(detectionPosition, new Vector3(AreaSize.x, AreaSize.y, 0.1f));

        if (_chaseReleaseMargin > 0f)
        {
            Gizmos.color = Color.yellow;
            Vector2 chaseSize = AreaSize + Vector2.one * (_chaseReleaseMargin * 2f);
            Gizmos.DrawWireCube(detectionPosition,
                new Vector3(chaseSize.x, chaseSize.y, 0.1f));
        }
    }
}
