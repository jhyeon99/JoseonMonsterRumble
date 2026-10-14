using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MovingPlatform))]
public sealed class MovingPlatformFlameVisual : MonoBehaviour
{
    private static readonly int MotionBendId = Shader.PropertyToID("_MotionBend");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    private static readonly int PhaseId = Shader.PropertyToID("_Phase");
    private const int EmbersPerFlame = 6;
    private const float MovementThreshold = 0.05f;

    [SerializeField, Min(0f)] private float _bendPerSpeed = 0.04f;
    [SerializeField, Range(0f, 0.25f)] private float _maxBend = 0.16f;
    [SerializeField, Min(0.01f)] private float _responseTime = 0.15f;
    [SerializeField, Min(0.01f)] private float _emberInterval = 0.16f;
    [SerializeField, Min(0.01f)] private float _emberLifetime = 0.5f;
    [SerializeField] private Vector2 _emberSizeRatio = new Vector2(0.07f, 0.14f);
    [SerializeField, Range(0f, 0.5f)] private float _movementBrightness = 0.1f;

    private MovingPlatform _platform;
    private readonly List<Flame> _flames = new List<Flame>();
    private readonly List<Ember> _embers = new List<Ember>();
    private bool _wasMoving;
    private float _brightness;

    private void Start()
    {
        _platform = GetComponent<MovingPlatform>();
        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>())
        {
            Material material = renderer.sharedMaterial;
            if (material == null || material.shader.name != "Team06/BlueGuideFlame") continue;
            if (renderer.sprite == null)
            {
                Debug.LogWarning($"{renderer.name}: 이동 불꽃의 Sprite 참조가 없습니다.", renderer);
                continue;
            }
            var flame = new Flame
            {
                Renderer = renderer,
                Intensity = material.GetFloat(IntensityId),
                Opacity = material.GetFloat(OpacityId),
                Properties = new MaterialPropertyBlock()
            };
            _flames.Add(flame);
            for (int i = 0; i < EmbersPerFlame; i++) CreateEmber(flame);
        }
        // 불꽃이 없는 이동 발판에는 효과를 실행하지 않는다.
        if (_flames.Count == 0) enabled = false;
    }

    private void LateUpdate()
    {
        if (Time.deltaTime <= 0f) return;
        Vector2 velocity = _platform.isActiveAndEnabled ? _platform.Velocity : Vector2.zero;
        bool moving = velocity.sqrMagnitude > MovementThreshold * MovementThreshold;
        float response = 1f - Mathf.Exp(-Time.deltaTime / _responseTime);
        _brightness = Mathf.Lerp(_brightness, moving ? _movementBrightness : 0f, response);
        foreach (Flame flame in _flames)
        {
            if (flame.Renderer == null || !flame.Renderer.enabled || !flame.Renderer.gameObject.activeInHierarchy) continue;
            Vector3 localVelocity = flame.Renderer.transform.InverseTransformDirection(velocity);
            if (flame.Renderer.flipX) localVelocity.x = -localVelocity.x;
            if (flame.Renderer.flipY) localVelocity.y = -localVelocity.y;
            var bend = Vector2.ClampMagnitude(-(Vector2)localVelocity * _bendPerSpeed, _maxBend);
            // 위로 타오르는 모양을 남기도록 세로 변형은 가로보다 작게 한다.
            bend.y *= 0.5f;
            flame.Bend = Vector2.Lerp(flame.Bend, bend, response);
            flame.Renderer.GetPropertyBlock(flame.Properties);
            flame.Properties.SetVector(MotionBendId, new Vector4(flame.Bend.x, flame.Bend.y, 0f, 0f));
            flame.Properties.SetFloat(IntensityId, flame.Intensity * (1f + _brightness));
            flame.Renderer.SetPropertyBlock(flame.Properties);
            if (moving != _wasMoving) EmitEmber(flame, velocity);
            flame.EmitTimer += Time.deltaTime;
            if (moving && flame.EmitTimer >= _emberInterval)
            {
                flame.EmitTimer %= _emberInterval;
                EmitEmber(flame, velocity);
            }
            if (!moving) flame.EmitTimer = 0f;
        }
        _wasMoving = moving;
        UpdateEmbers();
    }

    private void CreateEmber(Flame flame)
    {
        var emberObject = new GameObject("MovingFlameEmber");
        emberObject.transform.SetParent(transform, false);
        SpriteRenderer renderer = emberObject.AddComponent<SpriteRenderer>();
        renderer.sprite = flame.Renderer.sprite;
        renderer.sharedMaterial = flame.Renderer.sharedMaterial;
        renderer.sortingLayerID = flame.Renderer.sortingLayerID;
        renderer.sortingOrder = flame.Renderer.sortingOrder - 1;
        renderer.enabled = false;
        _embers.Add(new Ember { Renderer = renderer, Source = flame, Properties = new MaterialPropertyBlock() });
    }

    private void EmitEmber(Flame flame, Vector2 velocity)
    {
        foreach (Ember ember in _embers)
        {
            if (ember.Source != flame || ember.Renderer.enabled) continue;
            Bounds bounds = flame.Renderer.sprite.bounds;
            ember.Origin = flame.Renderer.transform.TransformPoint(bounds.center + Vector3.down * (bounds.size.y * 0.17f));
            ember.Velocity = -velocity * 0.06f + new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(0.1f, 0.3f));
            ember.Age = 0f;
            ember.Lifetime = _emberLifetime * Random.Range(0.75f, 1.2f);
            float ratio = Random.Range(_emberSizeRatio.x, _emberSizeRatio.y);
            Vector3 scale = flame.Renderer.transform.lossyScale;
            Vector3 parentScale = transform.lossyScale;
            ember.Scale = new Vector3(Mathf.Abs(scale.x / parentScale.x), Mathf.Abs(scale.y / parentScale.y), 1f) * ratio;
            ember.Renderer.transform.position = ember.Origin;
            ember.Renderer.transform.rotation = flame.Renderer.transform.rotation;
            ember.Renderer.transform.localScale = ember.Scale;
            ember.Properties.Clear();
            ember.Properties.SetFloat(IntensityId, flame.Intensity * 0.75f);
            ember.Properties.SetFloat(OpacityId, flame.Opacity * 0.65f);
            ember.Properties.SetFloat(PhaseId, Random.Range(0f, 10f));
            ember.Renderer.SetPropertyBlock(ember.Properties);
            ember.Renderer.enabled = true;
            return;
        }
    }

    private void UpdateEmbers()
    {
        foreach (Ember ember in _embers)
        {
            if (!ember.Renderer.enabled) continue;
            ember.Age += Time.deltaTime;
            float progress = Mathf.Clamp01(ember.Age / ember.Lifetime);
            ember.Renderer.transform.position = ember.Origin + (Vector3)(ember.Velocity * ember.Age);
            ember.Renderer.transform.localScale = ember.Scale * Mathf.Lerp(1f, 0.35f, progress);
            ember.Properties.SetFloat(OpacityId, ember.Source.Opacity * 0.65f * (1f - progress));
            ember.Renderer.SetPropertyBlock(ember.Properties);
            if (progress >= 1f) ember.Renderer.enabled = false;
        }
    }

    private void OnDisable()
    {
        _wasMoving = false;
        _brightness = 0f;
        foreach (Flame flame in _flames)
        {
            flame.Bend = Vector2.zero;
            flame.EmitTimer = 0f;
            if (flame.Renderer == null) continue;
            flame.Renderer.GetPropertyBlock(flame.Properties);
            flame.Properties.SetVector(MotionBendId, Vector4.zero);
            flame.Properties.SetFloat(IntensityId, flame.Intensity);
            flame.Renderer.SetPropertyBlock(flame.Properties);
        }
        foreach (Ember ember in _embers)
            if (ember.Renderer != null) ember.Renderer.enabled = false;
    }

    private void OnDestroy()
    {
        foreach (Ember ember in _embers)
            if (ember.Renderer != null) Destroy(ember.Renderer.gameObject);
    }

    private sealed class Flame
    {
        public SpriteRenderer Renderer;
        public MaterialPropertyBlock Properties;
        public float Intensity;
        public float Opacity;
        public Vector2 Bend;
        public float EmitTimer;
    }

    private sealed class Ember
    {
        public SpriteRenderer Renderer;
        public Flame Source;
        public MaterialPropertyBlock Properties;
        public Vector3 Origin;
        public Vector2 Velocity;
        public Vector3 Scale;
        public float Age;
        public float Lifetime;
    }
}
