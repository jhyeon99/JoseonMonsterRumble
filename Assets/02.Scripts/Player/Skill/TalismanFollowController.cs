using UnityEngine;

[DisallowMultipleComponent]
public sealed class TalismanFollowController : MonoBehaviour
{
    private const float FullCycleRadians = Mathf.PI * 2f;

    [Header("추종")]
    [SerializeField] private Vector2 _followOffset = new Vector2(-0.9f, 1.2f);
    [SerializeField, Min(0.01f)] private float _smoothTime = 0.18f;
    [SerializeField, Min(0f)] private float _maxFollowSpeed = 12f;
    [SerializeField, Min(0f)] private float _snapDistance = 4f;

    [Header("부유")]
    [SerializeField, Min(0f)] private float _floatAmplitude = 0.08f;
    [SerializeField, Min(0f)] private float _floatFrequency = 1.5f;
    [SerializeField] private float _phaseOffset = 1.1f;
    [SerializeField, Min(0f)] private float _rotationAmplitude = 3f;

    private Transform _target;
    private Transform[] _slots;
    private Vector3[] _baseLocalPositions;
    private Quaternion[] _baseLocalRotations;
    private Vector3 _followVelocity;
    private bool _isInitialized;

    public void Initialize(Transform target)
    {
        if (target == null)
        {
            Debug.LogWarning("[TalismanFollowController] 추종 대상이 없습니다.", this);
            return;
        }

        _target = target;
        CacheSlotTransforms();
        transform.SetParent(null, true);
        _isInitialized = true;
        SnapToTarget();
    }

    private void LateUpdate()
    {
        if (!_isInitialized || _target == null || Time.deltaTime <= 0f)
        {
            return;
        }

        FollowTarget();
        UpdateFloatingMotion();
    }

    private void CacheSlotTransforms()
    {
        int slotCount = transform.childCount;
        _slots = new Transform[slotCount];
        _baseLocalPositions = new Vector3[slotCount];
        _baseLocalRotations = new Quaternion[slotCount];

        for (int i = 0; i < slotCount; i++)
        {
            Transform slot = transform.GetChild(i);
            _slots[i] = slot;
            _baseLocalPositions[i] = slot.localPosition;
            _baseLocalRotations[i] = slot.localRotation;
        }
    }

    private void FollowTarget()
    {
        Vector3 targetPosition = _target.TransformPoint(_followOffset);
        float snapDistanceSqr = _snapDistance * _snapDistance;

        if ((targetPosition - transform.position).sqrMagnitude >= snapDistanceSqr)
        {
            transform.position = targetPosition;
            _followVelocity = Vector3.zero;
            return;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref _followVelocity,
            _smoothTime,
            _maxFollowSpeed,
            Time.deltaTime);
    }

    private void UpdateFloatingMotion()
    {
        float basePhase = Time.time * _floatFrequency * FullCycleRadians;

        for (int i = 0; i < _slots.Length; i++)
        {
            float wave = Mathf.Sin(basePhase + i * _phaseOffset);
            Vector3 position = _baseLocalPositions[i];
            position.y += wave * _floatAmplitude;

            _slots[i].localPosition = position;
            _slots[i].localRotation = _baseLocalRotations[i] * Quaternion.Euler(0f, 0f, wave * _rotationAmplitude);
        }
    }

    private void SnapToTarget()
    {
        if (_target == null)
        {
            return;
        }

        transform.position = _target.TransformPoint(_followOffset);
        _followVelocity = Vector3.zero;
    }
}
