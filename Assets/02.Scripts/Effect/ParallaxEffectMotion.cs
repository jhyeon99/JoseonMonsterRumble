using UnityEngine;

[DefaultExecutionOrder(110)]
[DisallowMultipleComponent]
public sealed class ParallaxEffectMotion : MonoBehaviour
{
    private const string DefaultLayerName = "Far Mountain";
    private const string MissingLayerWarning = "패링 소멸 VFX: 지정한 패럴랙스 레이어를 찾지 못해 월드 위치를 유지합니다.";

    [SerializeField] private string _backgroundLayerName = DefaultLayerName;

    private Transform _targetCamera;
    private ParallaxManager _cachedParallax;
    private Vector2 _cameraMovementMultiplier;
    private Vector3 _startCameraPosition;
    private Vector3 _startEffectPosition;
    private bool _hasWarnedMissingLayer;

    private void OnEnable()
    {
        _targetCamera = null;
        _cameraMovementMultiplier = Vector2.zero;
        _startEffectPosition = transform.position;
        Camera worldCamera = Camera.main;
        if (worldCamera == null)
        {
            return;
        }
        if (!TryInitializeCameraMovement(worldCamera.transform))
        {
            return;
        }
        _startCameraPosition = _targetCamera.position;
    }

    private bool TryInitializeCameraMovement(Transform cameraTransform)
    {
        if (TryUseParallaxManager(_cachedParallax, cameraTransform))
        {
            return true;
        }
        _cachedParallax = null;
        ParallaxManager[] parallaxes = FindObjectsByType<ParallaxManager>(FindObjectsSortMode.None);
        foreach (ParallaxManager parallax in parallaxes)
        {
            if (TryUseParallaxManager(parallax, cameraTransform))
            {
                _cachedParallax = parallax;
                return true;
            }
        }
        if (parallaxes.Length > 0 && !_hasWarnedMissingLayer)
        {
            Debug.LogWarning(MissingLayerWarning, this);
            _hasWarnedMissingLayer = true;
        }
        return false;
    }

    private bool TryUseParallaxManager(ParallaxManager parallax, Transform cameraTransform)
    {
        if (parallax == null ||
            !parallax.TryGetLayerCameraMovement(_backgroundLayerName, out Transform targetCamera, out Vector2 multiplier) ||
            targetCamera != cameraTransform)
        {
            return false;
        }
        _targetCamera = targetCamera;
        _cameraMovementMultiplier = multiplier;
        return true;
    }

    private void LateUpdate()
    {
        if (_targetCamera == null)
        {
            return;
        }
        Vector3 cameraMovement = _targetCamera.position - _startCameraPosition;
        transform.position = _startEffectPosition + new Vector3(
            cameraMovement.x * _cameraMovementMultiplier.x,
            cameraMovement.y * _cameraMovementMultiplier.y,
            0f);
    }

    private void OnDisable()
    {
        _targetCamera = null;
        _cameraMovementMultiplier = Vector2.zero;
        _startCameraPosition = Vector3.zero;
        _startEffectPosition = Vector3.zero;
    }
}
