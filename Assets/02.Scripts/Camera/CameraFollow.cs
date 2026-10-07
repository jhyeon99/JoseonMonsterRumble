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


    private Vector3 _velocity = Vector3.zero;

    // 따라가기 기준 위치. 흔들림 등 연출 오프셋(CameraShake)은 매 프레임 이 위에 얹혀서 누적되지 않음
    private Vector3 _basePosition;

    public Transform Target => _target;

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
        if (_target != null)
        {
            _basePosition = Vector3.SmoothDamp(_basePosition, GetTargetPosition(), ref _velocity, _smoothTime);
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
        int signX = _target.localScale.x > 0 ? 1 : -1;

        var realOffset = new Vector3(signX * _offset.x, _offset.y, _offset.z);
        Vector3 targetPosition = _target.position + realOffset;
        targetPosition.x = Mathf.Clamp(targetPosition.x, _minX, _maxX);
        targetPosition.y = Mathf.Clamp(targetPosition.y, _minY, _maxY);
        return targetPosition;
    }
}