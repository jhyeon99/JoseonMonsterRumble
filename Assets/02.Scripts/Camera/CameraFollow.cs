using UnityEngine;

[DefaultExecutionOrder(1)]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private Vector3 _offset = new Vector3(0f, 0f, -10f);
    [SerializeField] private float _smoothTime = 1f;

    [Header("Bound Settings")]
    [SerializeField] private float _minX = -10f;

    [SerializeField] private float _maxX = 10f;
    [SerializeField] private float _minY = -10f;
    [SerializeField] private float _maxY = 10f;


    private Vector3 _velocity = Vector3.zero;

    private void LateUpdate()
    {
        if (_target == null) return;

        transform.position =
            Vector3.SmoothDamp(transform.position, GetTargetPosition(), ref _velocity, _smoothTime);
    }

    /// <summary>부드럽게 따라가지 않고 바로 목표 위치로 이동 (리스폰 등 순간이동 직후 사용)</summary>
    public void SnapToTarget()
    {
        if (_target == null) return;

        transform.position = GetTargetPosition();
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