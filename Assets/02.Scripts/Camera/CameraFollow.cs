using UnityEngine;

[DefaultExecutionOrder(1)]
public class CameraFollow : MonoBehaviour
{
    [SerializeField]
    private Transform _target;

    [SerializeField]
    private Vector3 _offset = new Vector3(0f, 0f, -10f);

    [SerializeField]
    private float _smoothTime = 1f;

    [Header("Bound Settings")]
    [SerializeField]
    private float _minX = -10f;

    [SerializeField]
    private float _maxX = 10f;

    [SerializeField]
    private float _minY = -10f;

    [SerializeField]
    private float _maxY = 10f;

    [Tooltip("연출 중에도 화면 전체가 안에 머물러야 할 배경. 비우면 기존 연출 이동 유지")]
    [SerializeField] private SpriteRenderer _cameraViewBounds;

    public Vector3 ConstrainCameraEffectPosition(Vector3 basePosition, Vector3 effectOffset, Camera camera)
    {
        if (_cameraViewBounds == null || camera == null || !camera.orthographic)
            return basePosition + effectOffset;

        Bounds bounds = _cameraViewBounds.bounds;
        Vector3 right = camera.transform.right;
        Vector3 up = camera.transform.up;
        float halfWidth = camera.orthographicSize * (Mathf.Abs(right.x) * camera.aspect + Mathf.Abs(up.x));
        float halfHeight = camera.orthographicSize * (Mathf.Abs(right.y) * camera.aspect + Mathf.Abs(up.y));
        float minX = bounds.min.x + halfWidth;
        float maxX = bounds.max.x - halfWidth;
        float minY = bounds.min.y + halfHeight;
        float maxY = bounds.max.y - halfHeight;

        // 기준 위치는 그대로 두고 경계 방향의 연출 이동만 부드럽게 줄인다.
        Vector3 position = basePosition;
        position.x = minX <= maxX ? Mathf.Clamp(position.x, minX, maxX) : bounds.center.x;
        position.y = minY <= maxY ? Mathf.Clamp(position.y, minY, maxY) : bounds.center.y;
        position.x += LimitEffectOffset(effectOffset.x, effectOffset.x >= 0f ? maxX - position.x : position.x - minX);
        position.y += LimitEffectOffset(effectOffset.y, effectOffset.y >= 0f ? maxY - position.y : position.y - minY);
        position.z += effectOffset.z;
        return position;
    }

    private static float LimitEffectOffset(float offset, float availableDistance)
    {
        if (availableDistance <= 0f) return 0f;
        float magnitude = Mathf.Abs(offset);
        float softStart = availableDistance * 0.5f;
        if (magnitude <= softStart) return offset;
        float excess = magnitude - softStart;
        return Mathf.Sign(offset) * (softStart + softStart * excess / (softStart + excess));
    }


    private Vector3 _velocity = Vector3.zero;

    // 따라가기 기준 위치. 흔들림 등 연출 오프셋(CameraShake)은 매 프레임 이 위에 얹혀서 누적되지 않음
    private Vector3 _basePosition;

    // 연출용 초점 (보스 등장 등). 켜져 있으면 대상 대신 이 위치를 따라감
    private bool _hasFocus;
    private Vector3 _focusPoint;
    private float _smoothTimeOverride = -1f;

    public Transform Target => _target;

    /// <summary>연출 중 대상 대신 이 위치를 비춤. smoothTime이 0 이상이면 그 부드러움으로 이동</summary>
    public void SetFocus(Vector3 worldPoint, float smoothTime = -1f)
    {
        _hasFocus = true;
        _focusPoint = worldPoint;
        _smoothTimeOverride = smoothTime;
    }

    /// <summary>연출 초점 해제 후 원래 대상으로 복귀. smoothTime이 0 이상이면 도착할 때까지 그 부드러움 사용</summary>
    public void ClearFocus(float smoothTime = -1f)
    {
        _hasFocus = false;
        _smoothTimeOverride = smoothTime;
    }

    private void Awake()
    {
        _basePosition = transform.position;

        // 비어 있으면 Player 태그로 자동 연결 (씬마다 수동 연결 불필요)
        // StageManager가 리스폰 시 SnapToTarget을 부르기 전에 연결되도록 Awake에서 처리
        if (_target != null) return;

        var player = GameObject.FindWithTag(Constants.Tags.Player);
        if (player != null)
        {
            _target = player.transform;
        }
        else
        {
            Debug.LogWarning($"{name}: Player 태그 오브젝트를 찾지 못했습니다.", this);
        }
    }

    private void LateUpdate()
    {
        if (_target != null || _hasFocus)
        {
            Vector3 goal = GetTargetPosition();
            float smoothTime = _smoothTimeOverride >= 0f ? _smoothTimeOverride : _smoothTime;
            _basePosition = Vector3.SmoothDamp(_basePosition, goal, ref _velocity, smoothTime);

            // 초점 해제 후 원래 대상에 거의 도착하면 기본 부드러움으로
            if (!_hasFocus && _smoothTimeOverride >= 0f && (goal - _basePosition).sqrMagnitude < 0.0025f)
            {
                _smoothTimeOverride = -1f;
            }
        }

        transform.position = _basePosition;
    }

    /// <summary>부드럽게 따라가지 않고 바로 목표 위치로 이동 (리스폰 등 순간이동 직후 사용)</summary>
    public void SnapToTarget()
    {
        if (_target == null) return;

        _basePosition = GetTargetPosition();
        transform.position = _basePosition;
        _velocity = Vector3.zero;
    }

    private Vector3 GetTargetPosition()
    {
        if (_hasFocus)
        {
            var focus = new Vector3(_focusPoint.x, _focusPoint.y, _offset.z);
            focus.x = Mathf.Clamp(focus.x, _minX, _maxX);
            focus.y = Mathf.Clamp(focus.y, _minY, _maxY);
            return focus;
        }

        int signX = _target.localScale.x > 0 ? 1 : -1;

        var realOffset = new Vector3(signX * _offset.x, _offset.y, _offset.z);
        Vector3 targetPosition = _target.position + realOffset;
        targetPosition.x = Mathf.Clamp(targetPosition.x, _minX, _maxX);
        targetPosition.y = Mathf.Clamp(targetPosition.y, _minY, _maxY);
        return targetPosition;
    }
}
