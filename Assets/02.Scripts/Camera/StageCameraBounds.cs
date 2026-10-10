using Com.LuisPedroFonseca.ProCamera2D;
using UnityEngine;

[DefaultExecutionOrder(90)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[RequireComponent(typeof(ProCamera2D))]
[RequireComponent(typeof(ProCamera2DNumericBoundaries))]
public sealed class StageCameraBounds : MonoBehaviour
{
    [Header("맵 양 끝의 월드 X 좌표")]
    [Tooltip("카메라 화면의 왼쪽 가장자리가 넘어갈 수 없는 좌표입니다.")]
    [SerializeField] private float _leftX = -27.222221f;

    [Tooltip("카메라 화면의 오른쪽 가장자리가 넘어갈 수 없는 좌표입니다.")]
    [SerializeField] private float _rightX = 518.2222f;

    public float LeftX => _leftX;
    public float RightX => _rightX;

    private Camera _camera;
    private ProCamera2D _proCamera;
    private ProCamera2DNumericBoundaries _numericBoundaries;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _proCamera = GetComponent<ProCamera2D>();
        _numericBoundaries = GetComponent<ProCamera2DNumericBoundaries>();
    }

    private void OnEnable()
    {
        _proCamera.PreMoveUpdate += HandlePreMove;
        _proCamera.OnReset += ClampCamera;
        ApplyBoundaries();
    }

    private void OnDisable()
    {
        _proCamera.PreMoveUpdate -= HandlePreMove;
        _proCamera.OnReset -= ClampCamera;
    }

    private void OnValidate()
    {
        _rightX = Mathf.Max(_rightX, _leftX + 0.01f);
    }

    private void HandlePreMove(float deltaTime)
    {
        ApplyBoundaries();
    }

    private void LateUpdate()
    {
        ApplyBoundaries();
        ClampCamera();
    }

    private void ApplyBoundaries()
    {
        // Numeric Boundaries는 카메라 부모 기준 좌표를 사용한다.
        float parentX = transform.parent != null ? transform.parent.position.x : 0f;
        _numericBoundaries.UseNumericBoundaries = true;
        _numericBoundaries.UseLeftBoundary = true;
        _numericBoundaries.UseRightBoundary = true;
        _numericBoundaries.LeftBoundary = _leftX - parentX;
        _numericBoundaries.RightBoundary = _rightX - parentX;
        _numericBoundaries.TargetLeftBoundary = _numericBoundaries.LeftBoundary;
        _numericBoundaries.TargetRightBoundary = _numericBoundaries.RightBoundary;
    }

    private void ClampCamera()
    {
        if (!_camera.orthographic || _rightX <= _leftX)
            return;

        // 줌과 화면 비율뿐 아니라 흔들림으로 회전한 화면의 모서리도 포함한다.
        float extentPerSize = Mathf.Abs(_camera.transform.right.x) * _camera.aspect +
            Mathf.Abs(_camera.transform.up.x);
        if (extentPerSize <= 0f)
            return;

        float maximumSize = (_rightX - _leftX) * 0.5f / extentPerSize;
        if (_camera.orthographicSize > maximumSize)
        {
            if (_proCamera.isActiveAndEnabled)
                _proCamera.UpdateScreenSize(maximumSize);
            else
                _camera.orthographicSize = maximumSize;
        }

        float halfWidth = _camera.orthographicSize * extentPerSize;
        float minimumCenterX = _leftX + halfWidth;
        float maximumCenterX = _rightX - halfWidth;
        Vector3 position = transform.position;
        position.x = minimumCenterX <= maximumCenterX
            ? Mathf.Clamp(position.x, minimumCenterX, maximumCenterX)
            : (_leftX + _rightX) * 0.5f;
        transform.position = position;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(new Vector3(_leftX, -100f, 0f), new Vector3(_leftX, 100f, 0f));
        Gizmos.DrawLine(new Vector3(_rightX, -100f, 0f), new Vector3(_rightX, 100f, 0f));
    }
}
