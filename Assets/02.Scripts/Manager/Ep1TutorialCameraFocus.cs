using System.Collections.Generic;
using Com.LuisPedroFonseca.ProCamera2D;
using UnityEngine;
using UnityEngine.UI;

// Gameplay stays paused while this camera animation uses unscaled time.
[DefaultExecutionOrder(1000)]
public sealed class Ep1TutorialCameraFocus : MonoBehaviour
{
    private readonly List<Behaviour> _suspended = new List<Behaviour>();
    private Camera _camera;
    private Vector3 _originalPosition;
    private Quaternion _originalRotation;
    private float _originalSize;
    private Vector3 _focusPosition;
    private float _focusSize;
    private float _startedAt;
    private float _duration;
    private float _barAmount;
    private bool _active;
    private bool _returning;
    private RectTransform _bars;
    private RectTransform _topBar;
    private RectTransform _bottomBar;

    public bool HasReachedTarget { get; private set; }
    public bool HasReturned => !_active;

    public void Begin(Camera camera, Bounds target, float zoomFactor, float duration, float barAmount)
    {
        Cancel();
        _camera = camera;
        _originalPosition = camera.transform.position;
        _originalRotation = camera.transform.rotation;
        _originalSize = camera.orthographicSize;
        // Keep the real monster inside the clear area between the cinematic bars.
        float fitSize = Mathf.Max(target.extents.y / 0.55f,
            target.extents.x / Mathf.Max(0.1f, camera.aspect * 0.9f)) * 1.1f;
        _focusSize = Mathf.Max(0.5f, fitSize, _originalSize * zoomFactor);
        // Leave space below the real sprite for the explanation, also in a narrow viewport.
        _focusPosition = new Vector3(target.center.x, target.center.y - _focusSize * 0.24f, _originalPosition.z);
        _duration = Mathf.Max(0.01f, duration);
        _barAmount = barAmount;
        _startedAt = Time.unscaledTime;
        _returning = false;
        HasReachedTarget = false;
        Suspend(camera.GetComponent<ProCamera2D>());
        Suspend(camera.GetComponent<CameraFollow>());
        Suspend(camera.GetComponent<StageCameraBounds>());
        Suspend(camera.GetComponent<MovingPlatformCameraZoom>());
        _active = true;
        EnsureBars();
        _bars.gameObject.SetActive(true);
    }

    public void Return(float duration)
    {
        if (!_active || _returning) return;
        _focusPosition = _camera.transform.position;
        _focusSize = _camera.orthographicSize;
        _duration = Mathf.Max(0.01f, duration);
        _startedAt = Time.unscaledTime;
        _returning = true;
    }

    private void Update() => ApplyCamera();
    private void LateUpdate() => ApplyCamera();

    private void ApplyCamera()
    {
        if (!_active || _camera == null) return;
        float progress = Mathf.Clamp01((Time.unscaledTime - _startedAt) / _duration);
        float eased = Mathf.SmoothStep(0f, 1f, progress);
        _camera.transform.position = Vector3.Lerp(_originalPosition, _focusPosition, _returning ? 1f - eased : eased);
        _camera.transform.rotation = Quaternion.Slerp(_originalRotation, Quaternion.identity, _returning ? 1f - eased : eased);
        _camera.orthographicSize = Mathf.Lerp(_originalSize, _focusSize, _returning ? 1f - eased : eased);
        float height = _bars.rect.height * _barAmount * (_returning ? 1f - eased : eased);
        _topBar.sizeDelta = _bottomBar.sizeDelta = new Vector2(0f, height);
        if (progress < 1f) return;
        if (_returning) Cancel();
        else HasReachedTarget = true;
    }

    private void Suspend(Behaviour behaviour)
    {
        if (behaviour == null || !behaviour.enabled) return;
        _suspended.Add(behaviour);
        behaviour.enabled = false;
    }

    public void Cancel()
    {
        if (_active && _camera != null)
        {
            _camera.transform.SetPositionAndRotation(_originalPosition, _originalRotation);
            _camera.orthographicSize = _originalSize;
        }
        foreach (Behaviour behaviour in _suspended)
            if (behaviour != null) behaviour.enabled = true;
        _suspended.Clear();
        _active = false;
        _returning = false;
        HasReachedTarget = false;
        if (_bars != null) _bars.gameObject.SetActive(false);
    }

    private void EnsureBars()
    {
        if (_bars != null) return;
        var root = new GameObject("TalismanCinematicBars", typeof(RectTransform), typeof(Canvas));
        root.transform.SetParent(transform, false);
        _bars = root.GetComponent<RectTransform>();
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32764;
        canvas.pixelPerfect = true;
        _topBar = AddBar("Top", new Vector2(0f, 1f));
        _bottomBar = AddBar("Bottom", Vector2.zero);
    }

    private RectTransform AddBar(string name, Vector2 pivot)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Image));
        root.transform.SetParent(_bars, false);
        root.GetComponent<Image>().color = Color.black;
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = pivot;
        rect.anchorMax = new Vector2(1f, pivot.y);
        rect.pivot = new Vector2(0.5f, pivot.y);
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }

    private void OnDisable() => Cancel();
}
