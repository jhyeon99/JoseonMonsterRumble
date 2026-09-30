using UnityEngine;

[System.Serializable]
public struct CameraYZone
{
    public float minY;
    public float maxY;
    public float fixedY;
}

[DefaultExecutionOrder(1)]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private Vector3 _offset = new Vector3(0f, 0f, -10f);
    [SerializeField] private float _smoothTime = 1f;

    [Header("Bound Settings")]
    [SerializeField] private float _minX = -10f;

    [SerializeField] private float _maxX = 10f;

    [SerializeField] private CameraYZone[] _yZones;


    private Vector3 _velocity = Vector3.zero;

    private void LateUpdate()
    {
        if (_target == null) return;

        int signX = _target.localScale.x > 0 ? 1 : -1;

        var realOffset = new Vector3(signX * _offset.x, 0, _offset.z);
        Vector3 targetPosition = _target.position + realOffset;
        targetPosition.x = Mathf.Clamp(targetPosition.x, _minX, _maxX);

        foreach (var yZone in _yZones)
        {
            if (yZone.maxY >= targetPosition.y && yZone.minY <= targetPosition.y)
            {
                targetPosition.y = yZone.fixedY;
            }
        }

        transform.position =
            Vector3.SmoothDamp(transform.position, targetPosition, ref _velocity, _smoothTime);
    }
}