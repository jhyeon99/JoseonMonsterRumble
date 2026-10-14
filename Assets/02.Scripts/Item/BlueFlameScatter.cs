using UnityEngine;

/// <summary>획득 안내창의 일시정지 중에도 독립적으로 흩어지고 사라지는 불꽃.</summary>
public sealed class BlueFlameScatter : MonoBehaviour
{
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int AnimationTimeId = Shader.PropertyToID("_AnimationTime");
    private Vector3 _origin;
    private Vector2 _direction;
    private float _distance, _duration, _size, _elapsed;
    private SpriteRenderer _renderer;
    private MaterialPropertyBlock _properties;

    public void Initialize(Vector2 direction, float distance, float duration, float size)
    {
        _origin = transform.position;
        _direction = direction;
        _distance = distance;
        _duration = Mathf.Max(0.1f, duration);
        _size = size;
        transform.localScale = new Vector3(size, size * 1.2f, 1f);
        _renderer = GetComponent<SpriteRenderer>();
        _properties = new MaterialPropertyBlock();
    }

    private void Update()
    {
        _elapsed += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(_elapsed / _duration);
        float travel = 1f - Mathf.Pow(1f - progress, 3f);
        Vector2 offset = _direction * (_distance * travel) + Vector2.up * (0.25f * progress * progress);
        transform.position = _origin + new Vector3(offset.x, offset.y, 0f);
        float scale = _size * Mathf.Lerp(1f, 0.2f, progress);
        transform.localScale = new Vector3(scale, scale * 1.2f, 1f);
        if (_renderer != null)
        {
            _renderer.GetPropertyBlock(_properties);
            _properties.SetFloat(OpacityId, 1f - Mathf.SmoothStep(0f, 1f, progress));
            _properties.SetFloat(IntensityId, Mathf.Lerp(2f, 0.6f, progress));
            _properties.SetFloat(AnimationTimeId, Time.unscaledTime);
            _renderer.SetPropertyBlock(_properties);
        }
        if (progress >= 1f) Destroy(gameObject);
    }
}
