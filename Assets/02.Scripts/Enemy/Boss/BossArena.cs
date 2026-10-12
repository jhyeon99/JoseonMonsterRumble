using UnityEngine;

/// <summary>
/// 보스 맵 정보. 층 기준점(1 / 2 / 3층 바닥 높이)과 좌우 경계.
/// 패턴은 플레이어가 있는 층을 보고 선택됨.
/// </summary>
public class BossArena : MonoBehaviour
{
    [Tooltip("층 바닥 기준점. 아래층부터 순서대로 (0 = 1층, 1 = 2층, 2 = 3층). 빈 오브젝트를 각 층 바닥 높이에 두면 됨")]
    [SerializeField]
    private Transform[] _floors = new Transform[3];

    [Tooltip("맵 왼쪽 끝 (돌진, 점프 위치 제한)")]
    [SerializeField]
    private Transform _leftBound;

    [Tooltip("맵 오른쪽 끝")]
    [SerializeField]
    private Transform _rightBound;

    [Tooltip("층 판정 여유. 플레이어 위치가 층 바닥보다 이만큼 아래여도 그 층으로 봄")]
    [SerializeField, Min(0f)]
    private float _floorTolerance = 0.5f;

    public float LeftX => _leftBound != null ? _leftBound.position.x : float.NegativeInfinity;
    public float RightX => _rightBound != null ? _rightBound.position.x : float.PositiveInfinity;

    /// <summary>위치가 몇 층인지 (1 ~ 3)</summary>
    public int GetFloor(Vector2 position)
    {
        int floor = 1;

        for (int i = 0; i < _floors.Length; i++)
        {
            if (_floors[i] != null && position.y >= _floors[i].position.y - _floorTolerance)
            {
                floor = i + 1;
            }
        }

        return floor;
    }

    /// <summary>층 바닥 높이. 기준점이 없으면 0</summary>
    public float GetFloorY(int floor)
    {
        int index = floor - 1;
        if (index >= 0 && index < _floors.Length && _floors[index] != null)
        {
            return _floors[index].position.y;
        }

        return 0f;
    }

    /// <summary>층 바닥 높이. 기준점이 지정돼 있을 때만 true</summary>
    public bool TryGetFloorY(int floor, out float y)
    {
        int index = floor - 1;
        if (index >= 0 && index < _floors.Length && _floors[index] != null)
        {
            y = _floors[index].position.y;
            return true;
        }

        y = 0f;
        return false;
    }

    /// <summary>x를 맵 안으로 제한 (양 끝에서 margin만큼 안쪽)</summary>
    public float ClampX(float x, float margin = 0f)
    {
        float min = LeftX + margin;
        float max = RightX - margin;
        if (min > max)
        {
            return (min + max) * 0.5f;
        }

        return Mathf.Clamp(x, min, max);
    }

    public static BossFloorMask ToMask(int floor)
    {
        return floor >= 1 && floor <= 3 ? (BossFloorMask)(1 << (floor - 1)) : BossFloorMask.None;
    }

    private void OnDrawGizmos()
    {
        float left = _leftBound != null ? _leftBound.position.x : transform.position.x - 10f;
        float right = _rightBound != null ? _rightBound.position.x : transform.position.x + 10f;

        Color[] colors = { new Color(0.3f, 1f, 0.3f), new Color(1f, 0.9f, 0.2f), new Color(1f, 0.4f, 0.3f) };

        for (int i = 0; i < _floors.Length; i++)
        {
            if (_floors[i] == null)
            {
                continue;
            }

            float y = _floors[i].position.y;
            Gizmos.color = colors[i % colors.Length];
            Gizmos.DrawLine(new Vector3(left, y), new Vector3(right, y));

            Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.3f);
            Gizmos.DrawLine(new Vector3(left, y - _floorTolerance), new Vector3(right, y - _floorTolerance));
        }

        Gizmos.color = Color.cyan;
        if (_leftBound != null)
        {
            Gizmos.DrawLine(_leftBound.position + Vector3.down * 20f, _leftBound.position + Vector3.up * 20f);
        }

        if (_rightBound != null)
        {
            Gizmos.DrawLine(_rightBound.position + Vector3.down * 20f, _rightBound.position + Vector3.up * 20f);
        }
    }
}
