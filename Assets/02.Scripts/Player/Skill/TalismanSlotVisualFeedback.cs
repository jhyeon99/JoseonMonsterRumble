using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class TalismanSlotVisualFeedback : MonoBehaviour
{
    private const float FullCycleRadians = Mathf.PI * 2f;

    [Header("활성화 반응")]
    [SerializeField, Min(0.01f)] private float _flashDuration = 0.18f;
    [SerializeField]
    private AnimationCurve _scaleCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(0.35f, 1.3f),
        new Keyframe(1f, 1f));
    [SerializeField, ColorUsage(true, true)] private Color _flashColor = new Color(2f, 1.8f, 1.1f, 1f);
    [SerializeField] private Color _activeColor = new Color(1f, 0.9f, 0.55f, 1f);

    [Header("활성 대기")]
    [SerializeField, Min(0f)] private float _pulseScale = 0.06f;
    [SerializeField, Min(0f)] private float _pulseFrequency = 2f;

    private SpriteRenderer _spriteRenderer;
    private Vector3 _baseScale;
    private Color _baseColor;
    private float _elapsedTime;
    private bool _isActive;

    public void PlayActivation()
    {
        _elapsedTime = 0f;
        _isActive = true;
        ApplyFeedback(0f);
    }

    public void StopActivation()
    {
        _isActive = false;
        ResetVisuals();
    }

    private void Awake()
    {
        InitializeComponents();
    }

    private void Update()
    {
        if (!_isActive)
        {
            return;
        }

        _elapsedTime += Time.deltaTime;
        ApplyFeedback(_elapsedTime);
    }

    private void OnDisable()
    {
        _isActive = false;
        ResetVisuals();
    }

    private void InitializeComponents()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _baseScale = transform.localScale;
        _baseColor = _spriteRenderer.color;
    }

    private void ApplyFeedback(float elapsedTime)
    {
        if (elapsedTime < _flashDuration)
        {
            float normalizedTime = elapsedTime / _flashDuration;
            float scaleMultiplier = _scaleCurve.Evaluate(normalizedTime);
            transform.localScale = _baseScale * scaleMultiplier;
            _spriteRenderer.color = Color.Lerp(_flashColor, _activeColor, normalizedTime);
            return;
        }

        float pulse = Mathf.Sin(elapsedTime * _pulseFrequency * FullCycleRadians);
        transform.localScale = _baseScale * (1f + pulse * _pulseScale);
        _spriteRenderer.color = _activeColor;
    }

    private void ResetVisuals()
    {
        if (_spriteRenderer == null)
        {
            return;
        }

        transform.localScale = _baseScale;
        _spriteRenderer.color = _baseColor;
    }
}
