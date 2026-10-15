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

    [Tooltip("켜면 Scale Y 높이 안에 있는 플레이어에게만 적용합니다. 위아래로 겹친 길을 구분할 때 사용합니다.")]
    [SerializeField] private bool _useVerticalRange;

    [Tooltip("비워두면 기존 상단 경계를 유지합니다. 윗길에서는 고정된 화면 상단 경계를 연결합니다.")]
    [SerializeField] private Transform _topEdge;

    [Tooltip("다음 구간의 하단 경계로 전환하는 부드러움입니다. 클수록 천천히 이동하며, 0이면 즉시 적용합니다.")]
    [SerializeField, Min(0f), InspectorName("경계 전환 시간")]
    private float _transitionSmoothTime = 0.5f;

    [Tooltip("구간 사이의 짧은 틈에서는 이 시간 동안 이전 하단 제한을 유지합니다.")]
    [SerializeField, Min(0f), InspectorName("구간 이탈 대기 시간")]
    private float _exitGraceTime = 0.2f;

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
            _proCamera.OnReset += _state.ResetTransition;
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
            _state.Release();
            if (_state.Camera != null)
            {
                _state.Camera.PreMoveUpdate -= _state.UpdateBoundary;
                _state.Camera.OnReset -= _state.ResetTransition;
            }
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
        private bool _ownsTopBoundary;
        private bool _previousUseTopBoundary;
        private float _previousTopBoundary;
        private float _previousTargetTopBoundary;
        private float _currentWorldBottom;
        private float _targetWorldBottom;
        private float _transitionVelocity;
        private float _transitionSmoothTime;
        private float _exitGraceTime;
        private float _outsideTime;
        private bool _hasUpdated;

        public CameraBoundaryState(ProCamera2D camera, ProCamera2DNumericBoundaries boundaries)
        {
            Camera = camera;
            _boundaries = boundaries;
        }

        public void UpdateBoundary(float deltaTime)
        {
            bool snap = !_hasUpdated;
            if (deltaTime > 0f) _hasUpdated = true;
            if (Camera == null || _boundaries == null)
            {
                Release();
                return;
            }

            bool hasValidZone = false;
            bool insideAnyZone = false;
            float worldBottom = float.NegativeInfinity;
            CameraBottomBoundaryZone highestZone = null;
            foreach (CameraBottomBoundaryZone zone in Zones)
            {
                if (zone == null || !zone.isActiveAndEnabled || zone._player == null || zone._bottomEdge == null)
                    continue;

                hasValidZone = true;
                float halfWidth = Mathf.Abs(zone.transform.lossyScale.x) * 0.5f;
                if (Mathf.Abs(zone._player.position.x - zone.transform.position.x) > halfWidth)
                    continue;
                if (zone._useVerticalRange &&
                    Mathf.Abs(zone._player.position.y - zone.transform.position.y) > Mathf.Abs(zone.transform.lossyScale.y) * 0.5f)
                {
                    continue;
                }

                insideAnyZone = true;
                if (highestZone == null || zone._bottomEdge.position.y > worldBottom)
                {
                    highestZone = zone;
                    worldBottom = zone._bottomEdge.position.y;
                }
            }

            if (!insideAnyZone)
            {
                _outsideTime += Mathf.Max(0f, deltaTime);
                if (!_ownsBoundary || !hasValidZone || _outsideTime >= _exitGraceTime)
                {
                    Release();
                    return;
                }

                ApplyBoundary(deltaTime, snap);
                return;
            }

            _outsideTime = 0f;
            _transitionSmoothTime = Mathf.Max(0f, highestZone._transitionSmoothTime);
            _exitGraceTime = Mathf.Max(0f, highestZone._exitGraceTime);
            // Capture once for the camera, so zones cannot snapshot each other's limits.
            if (!_ownsBoundary)
            {
                _previousUseNumericBoundaries = _boundaries.UseNumericBoundaries;
                _previousUseBottomBoundary = _boundaries.UseBottomBoundary;
                _previousBottomBoundary = _boundaries.BottomBoundary;
                _previousTargetBottomBoundary = _boundaries.TargetBottomBoundary;
                _ownsBoundary = true;
                // A later entry starts from the currently visible bottom rather than snapping the camera.
                UnityEngine.Camera view = Camera.GameCamera;
                float extentPerSize = Mathf.Abs(view.transform.right.y) * view.aspect +
                    Mathf.Abs(view.transform.up.y);
                _currentWorldBottom = view.transform.position.y - view.orthographicSize * extentPerSize;
                _transitionVelocity = 0f;
            }

            float parentY = Camera.transform.parent != null ? Camera.transform.parent.position.y : 0f;
            if (_previousUseNumericBoundaries && _previousUseBottomBoundary)
                worldBottom = Mathf.Max(worldBottom, _previousBottomBoundary + parentY);

            _targetWorldBottom = worldBottom;
            if (highestZone._topEdge != null)
            {
                if (!_ownsTopBoundary)
                {
                    _previousUseTopBoundary = _boundaries.UseTopBoundary;
                    _previousTopBoundary = _boundaries.TopBoundary;
                    _previousTargetTopBoundary = _boundaries.TargetTopBoundary;
                    _ownsTopBoundary = true;
                }
                _boundaries.UseTopBoundary = true;
                _boundaries.TopBoundary = highestZone._topEdge.position.y - parentY;
                _boundaries.TargetTopBoundary = _boundaries.TopBoundary;
            }
            else
            {
                RestoreTopBoundary();
            }
            ApplyBoundary(deltaTime, snap);
        }

        private void ApplyBoundary(float deltaTime, bool snap)
        {
            if (snap || _transitionSmoothTime <= 0f)
            {
                _currentWorldBottom = _targetWorldBottom;
                _transitionVelocity = 0f;
            }
            else if (deltaTime > 0f)
            {
                _currentWorldBottom = Mathf.SmoothDamp(_currentWorldBottom, _targetWorldBottom,
                    ref _transitionVelocity, _transitionSmoothTime, Mathf.Infinity, deltaTime);
            }

            float parentY = Camera.transform.parent != null ? Camera.transform.parent.position.y : 0f;
            if (_previousUseNumericBoundaries && _previousUseBottomBoundary)
                _currentWorldBottom = Mathf.Max(_currentWorldBottom, _previousBottomBoundary + parentY);

            _boundaries.UseNumericBoundaries = true;
            _boundaries.UseBottomBoundary = true;
            _boundaries.BottomBoundary = _currentWorldBottom - parentY;
            _boundaries.TargetBottomBoundary = _targetWorldBottom - parentY;
        }

        public void ResetTransition()
        {
            // Respawns and explicit camera resets should establish the new floor immediately.
            _hasUpdated = false;
            _outsideTime = 0f;
            _transitionVelocity = 0f;
        }

        public void Release()
        {
            if (!_ownsBoundary)
                return;

            if (_boundaries != null)
            {
                // The native follow target keeps moving behind a hard boundary. Resume easing
                // from the visible camera position so releasing the floor cannot jump to it.
                if (Camera != null && _transitionSmoothTime > 0f)
                    Camera.ResetMovement();

                _boundaries.UseNumericBoundaries = _previousUseNumericBoundaries;
                _boundaries.UseBottomBoundary = _previousUseBottomBoundary;
                _boundaries.BottomBoundary = _previousBottomBoundary;
                _boundaries.TargetBottomBoundary = _previousTargetBottomBoundary;
                RestoreTopBoundary();
            }

            _ownsBoundary = false;
            _transitionVelocity = 0f;
            _outsideTime = 0f;
        }

        private void RestoreTopBoundary()
        {
            if (!_ownsTopBoundary) return;
            _boundaries.UseTopBoundary = _previousUseTopBoundary;
            _boundaries.TopBoundary = _previousTopBoundary;
            _boundaries.TargetTopBoundary = _previousTargetTopBoundary;
            _ownsTopBoundary = false;
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
        UnityEditor.Handles.Label(center + Vector3.up * (size.y * 0.5f),
            _useVerticalRange ? "이 영역에서 화면 상하단 제한 적용" : "이 X 구간에서 화면 하단 제한 적용");

        if (_bottomEdge != null)
        {
            var left = new Vector3(center.x - size.x * 0.5f,
                _bottomEdge.position.y, center.z);
            UnityEditor.Handles.Label(left,
                "화면 하단 한계 Y = " + _bottomEdge.position.y.ToString("0.##"));
        }
        if (_topEdge != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(new Vector3(center.x - size.x * 0.5f, _topEdge.position.y, center.z),
                new Vector3(center.x + size.x * 0.5f, _topEdge.position.y, center.z));
        }
    }
#endif
}
