using Com.LuisPedroFonseca.ProCamera2D;
using System.Collections.Generic;
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

    private static readonly Dictionary<ProCamera2D, CameraBoundaryState> States =
        new Dictionary<ProCamera2D, CameraBoundaryState>();

    private CameraBoundaryState _state;

    private void OnEnable()
    {
        if (_proCamera == null)
            return;

        if (!States.TryGetValue(_proCamera, out _state))
        {
            ProCamera2DNumericBoundaries boundaries = _proCamera.GetComponent<ProCamera2DNumericBoundaries>();
            if (boundaries == null)
                return;

            _state = new CameraBoundaryState(_proCamera, boundaries);
            States.Add(_proCamera, _state);
            _proCamera.PreMoveUpdate += _state.UpdateBoundary;
        }

        _state.Zones.Add(this);
    }

    private void OnDisable()
    {
        if (_state == null)
            return;

        _state.Zones.Remove(this);
        _state.UpdateBoundary(0f);
        if (_state.Zones.Count == 0)
        {
            if (_state.Camera != null)
                _state.Camera.PreMoveUpdate -= _state.UpdateBoundary;
            States.Remove(_state.Camera);
        }

        _state = null;
    }

    private sealed class CameraBoundaryState
    {
        public readonly ProCamera2D Camera;
        public readonly List<CameraBottomBoundaryZone> Zones = new List<CameraBottomBoundaryZone>();
        private readonly ProCamera2DNumericBoundaries _boundaries;
        private bool _ownsBoundary;
        private bool _previousUseNumericBoundaries;
        private bool _previousUseBottomBoundary;
        private float _previousBottomBoundary;
        private float _previousTargetBottomBoundary;

        public CameraBoundaryState(ProCamera2D camera, ProCamera2DNumericBoundaries boundaries)
        {
            Camera = camera;
            _boundaries = boundaries;
        }

        public void UpdateBoundary(float deltaTime)
        {
            bool insideAnyZone = false;
            float worldBottom = float.NegativeInfinity;
            foreach (CameraBottomBoundaryZone zone in Zones)
            {
                if (zone == null || !zone.isActiveAndEnabled || zone._player == null || zone._bottomEdge == null)
                    continue;

                float halfWidth = Mathf.Abs(zone.transform.lossyScale.x) * 0.5f;
                if (Mathf.Abs(zone._player.position.x - zone.transform.position.x) > halfWidth)
                    continue;

                insideAnyZone = true;
                worldBottom = Mathf.Max(worldBottom, zone._bottomEdge.position.y);
            }

            if (!insideAnyZone || Camera == null || _boundaries == null)
            {
                RestoreBoundary();
                return;
            }

            // Capture once for the camera, so zones cannot snapshot each other's limits.
            if (!_ownsBoundary)
            {
                _previousUseNumericBoundaries = _boundaries.UseNumericBoundaries;
                _previousUseBottomBoundary = _boundaries.UseBottomBoundary;
                _previousBottomBoundary = _boundaries.BottomBoundary;
                _previousTargetBottomBoundary = _boundaries.TargetBottomBoundary;
                _ownsBoundary = true;
            }

            float parentY = Camera.transform.parent != null ? Camera.transform.parent.position.y : 0f;
            float localBottom = worldBottom - parentY;
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
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (_bottomEdge == null)
            return;

        float halfWidth = Mathf.Abs(transform.lossyScale.x) * 0.5f;
        var left = new Vector3(transform.position.x - halfWidth,
            _bottomEdge.position.y, transform.position.z);
        var right = new Vector3(transform.position.x + halfWidth,
            _bottomEdge.position.y, transform.position.z);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(left, right);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;
        var size = new Vector3(Mathf.Abs(transform.lossyScale.x),
            Mathf.Max(1f, Mathf.Abs(transform.lossyScale.y)), 0f);
        Gizmos.color = new Color(0.2f, 1f, 0.45f, 0.08f);
        Gizmos.DrawCube(center, size);
        Gizmos.color = new Color(0.2f, 1f, 0.45f, 1f);
        Gizmos.DrawWireCube(center, size);
        UnityEditor.Handles.Label(center + Vector3.up * (size.y * 0.5f), "이 X 구간에서 화면 하단 제한 적용");

        if (_bottomEdge != null)
        {
            var left = new Vector3(center.x - size.x * 0.5f,
                _bottomEdge.position.y, center.z);
            UnityEditor.Handles.Label(left,
                "화면 하단 한계 Y = " + _bottomEdge.position.y.ToString("0.##"));
        }
    }
#endif
}
