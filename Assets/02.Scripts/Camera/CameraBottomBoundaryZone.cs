using Com.LuisPedroFonseca.ProCamera2D;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CameraBottomBoundaryZone : MonoBehaviour
{
    [Header("구간은 Transform의 X 위치와 Scale X로 조정합니다")]
    [SerializeField, InspectorName("카메라")]
    private ProCamera2D _proCamera;

    [SerializeField, InspectorName("플레이어")]
    private Transform _player;

    [Tooltip("이 오브젝트를 위아래로 움직이면 카메라 화면의 하단 한계가 바뀝니다.")]
    [SerializeField, InspectorName("화면 하단 경계")]
    private Transform _bottomEdge;

    private ProCamera2DNumericBoundaries _boundaries;
    private bool _ownsBoundary;
    private bool _previousUseNumericBoundaries;
    private bool _previousUseBottomBoundary;
    private float _previousBottomBoundary;
    private float _previousTargetBottomBoundary;

    private void OnEnable()
    {
        if (_proCamera == null)
            return;

        _boundaries = _proCamera.GetComponent<ProCamera2DNumericBoundaries>();
        if (_boundaries != null)
            _proCamera.PreMoveUpdate += UpdateBoundary;
    }

    private void OnDisable()
    {
        if (_proCamera != null)
            _proCamera.PreMoveUpdate -= UpdateBoundary;

        RestoreBoundary();
    }

    private void UpdateBoundary(float deltaTime)
    {
        if (_player == null || _bottomEdge == null || _boundaries == null)
        {
            RestoreBoundary();
            return;
        }

        float halfWidth = Mathf.Abs(transform.lossyScale.x) * 0.5f;
        bool inside = Mathf.Abs(_player.position.x - transform.position.x) <= halfWidth;
        if (!inside)
        {
            RestoreBoundary();
            return;
        }

        if (!_ownsBoundary)
        {
            _previousUseNumericBoundaries = _boundaries.UseNumericBoundaries;
            _previousUseBottomBoundary = _boundaries.UseBottomBoundary;
            _previousBottomBoundary = _boundaries.BottomBoundary;
            _previousTargetBottomBoundary = _boundaries.TargetBottomBoundary;
            _ownsBoundary = true;
        }

        // ProCamera2D uses the camera parent's coordinates for numeric boundaries.
        float parentY = _proCamera.transform.parent != null
            ? _proCamera.transform.parent.position.y
            : 0f;
        float localBottom = _bottomEdge.position.y - parentY;
        if (_previousUseNumericBoundaries && _previousUseBottomBoundary)
            localBottom = Mathf.Max(localBottom, _previousBottomBoundary);

        _boundaries.UseNumericBoundaries = true;
        _boundaries.UseBottomBoundary = true;
        _boundaries.BottomBoundary = localBottom;
        _boundaries.TargetBottomBoundary = localBottom;
    }

    private void RestoreBoundary()
    {
        if (!_ownsBoundary)
            return;

        if (_boundaries != null)
        {
            _boundaries.UseNumericBoundaries = _previousUseNumericBoundaries;
            _boundaries.UseBottomBoundary = _previousUseBottomBoundary;
            _boundaries.BottomBoundary = _previousBottomBoundary;
            _boundaries.TargetBottomBoundary = _previousTargetBottomBoundary;
        }

        _ownsBoundary = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (_bottomEdge == null)
            return;

        float halfWidth = Mathf.Abs(transform.lossyScale.x) * 0.5f;
        Vector3 left = new Vector3(transform.position.x - halfWidth,
            _bottomEdge.position.y, transform.position.z);
        Vector3 right = new Vector3(transform.position.x + halfWidth,
            _bottomEdge.position.y, transform.position.z);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(left, right);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;
        Vector3 size = new Vector3(Mathf.Abs(transform.lossyScale.x),
            Mathf.Max(1f, Mathf.Abs(transform.lossyScale.y)), 0f);
        Gizmos.color = new Color(0.2f, 1f, 0.45f, 0.08f);
        Gizmos.DrawCube(center, size);
        Gizmos.color = new Color(0.2f, 1f, 0.45f, 1f);
        Gizmos.DrawWireCube(center, size);
        UnityEditor.Handles.Label(center + Vector3.up * (size.y * 0.5f),
            "이 X 구간에서 화면 하단 제한 적용");

        if (_bottomEdge != null)
        {
            Vector3 left = new Vector3(center.x - size.x * 0.5f,
                _bottomEdge.position.y, center.z);
            UnityEditor.Handles.Label(left,
                "화면 하단 한계 Y = " + _bottomEdge.position.y.ToString("0.##"));
        }
    }
#endif
}
