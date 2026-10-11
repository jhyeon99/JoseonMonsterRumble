using System.Collections.Generic;
using UnityEngine;

public class PlayerDodgeAfterimage : MonoBehaviour
{
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
    private static readonly int TintStrengthId = Shader.PropertyToID("_TintStrength");
    private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");

    [Header("기본 잔상 설정")]
    [SerializeField] private SpriteRenderer _source;
    [SerializeField, Min(0.01f)] private float _spawnInterval = 0.05f;
    [SerializeField, Min(0.01f)] private float _lifetime = 0.25f;
    [SerializeField] private Color _color = new Color(0.5f, 0.8f, 1f, 0.45f);
    [SerializeField, Min(1)] private int _maxImages = 16;
    [SerializeField] private Material _glowMaterial;

    [Header("회피 성공 글로우 속도 곡선")]
    [SerializeField] private Color _successGlowColor = new Color(0.92f, 0.97f, 1f, 1f);
    [SerializeField, Min(0.05f)] private float _glowDuration = 0.28f;
    [SerializeField, Min(1f)] private float _maxBrightness = 4.2f;
    [SerializeField, Range(0f, 1f)] private float _maxTintStrength = 0.95f;
    [SerializeField]
    private AnimationCurve _glowDecayCurve = new AnimationCurve(
        new Keyframe(0f, 1f, -5f, -5f),
        new Keyframe(0.2f, 0.15f, -0.8f, -0.8f),
        new Keyframe(1f, 0f, 0f, 0f)
    );

    private sealed class Image
    {
        public SpriteRenderer Renderer;
        public float Remaining;
        public float Lifetime;
        public Color Color;
    }

    private readonly List<Image> _images = new List<Image>();
    private MaterialPropertyBlock _propBlock;
    private bool _emitting;
    private float _timer;
    private bool _isGlowActive;
    private float _glowElapsed;

    private void Awake()
    {
        if (_source == null)
        {
            _source = GetComponentInChildren<SpriteRenderer>();
        }

        _propBlock = new MaterialPropertyBlock();

        if (_glowMaterial == null)
        {
            var glowShader = Shader.Find("Team06/ProjectileGlow");
            if (glowShader != null)
            {
                _glowMaterial = new Material(glowShader);
            }
        }
    }

    public void Begin()
    {
        if (!isActiveAndEnabled) return;
        _emitting = true;
        _timer = 0f;
        _isGlowActive = false;
        _glowElapsed = 0f;
    }

    public void Stop()
    {
        _emitting = false;
    }

    public void TriggerDodgeSuccessGlow()
    {
        if (!isActiveAndEnabled) return;

        _isGlowActive = true;
        _glowElapsed = 0f;

        Emit(successPeak: true);
    }

    private void LateUpdate()
    {
        float currentBrightness = 1.0f;
        float currentTintStrength = 0.2f;
        Color currentTintColor = _color;

        if (_isGlowActive)
        {
            _glowElapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(_glowElapsed / Mathf.Max(0.01f, _glowDuration));
            float curveVal = _glowDecayCurve != null ? _glowDecayCurve.Evaluate(progress) : (1f - progress);

            currentBrightness = Mathf.Lerp(1.0f, _maxBrightness, curveVal);
            currentTintStrength = Mathf.Lerp(0.2f, _maxTintStrength, curveVal);
            currentTintColor = Color.Lerp(_color, _successGlowColor, curveVal);

            if (_glowElapsed >= _glowDuration)
            {
                _isGlowActive = false;
            }
        }

        foreach (Image image in _images)
        {
            if (image.Remaining <= 0f) continue;
            image.Remaining = Mathf.Max(0f, image.Remaining - Time.deltaTime);

            Color color = image.Color;
            color.a *= image.Remaining / image.Lifetime;
            image.Renderer.color = color;

            if (_glowMaterial != null && _propBlock != null)
            {
                image.Renderer.GetPropertyBlock(_propBlock);
                _propBlock.SetFloat(BrightnessId, currentBrightness);
                _propBlock.SetFloat(TintStrengthId, currentTintStrength);
                _propBlock.SetColor(TintColorId, currentTintColor);
                image.Renderer.SetPropertyBlock(_propBlock);
            }

            if (image.Remaining <= 0f)
            {
                image.Renderer.gameObject.SetActive(false);
            }
        }

        if (!_emitting) return;
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = Mathf.Max(0.01f, _spawnInterval);
        Emit(successPeak: false);
    }

    private void Emit(bool successPeak)
    {
        if (_source == null || !_source.enabled || !_source.gameObject.activeInHierarchy || _source.sprite == null)
            return;

        Image image = GetAvailableImage();
        if (image == null) return;

        SpriteRenderer renderer = image.Renderer;
        renderer.transform.SetPositionAndRotation(_source.transform.position, _source.transform.rotation);

        renderer.transform.localScale = _source.transform.lossyScale;
        renderer.sprite = _source.sprite;
        renderer.sharedMaterial = _glowMaterial != null ? _glowMaterial : _source.sharedMaterial;
        renderer.flipX = _source.flipX;
        renderer.flipY = _source.flipY;
        renderer.drawMode = _source.drawMode;
        renderer.size = _source.size;
        renderer.sortingLayerID = _source.sortingLayerID;
        renderer.sortingOrder = _source.sortingOrder - 1;

        Color startColor = successPeak ? _successGlowColor : _color;
        renderer.color = startColor;
        renderer.gameObject.SetActive(true);

        image.Color = startColor;
        image.Lifetime = Mathf.Max(0.01f, _lifetime);
        image.Remaining = image.Lifetime;

        if (_glowMaterial != null && _propBlock != null)
        {
            float brightness = successPeak ? _maxBrightness : 1.0f;
            float tintStrength = successPeak ? _maxTintStrength : 0.2f;
            Color tintColor = successPeak ? _successGlowColor : _color;

            _propBlock.SetFloat(BrightnessId, brightness);
            _propBlock.SetFloat(TintStrengthId, tintStrength);
            _propBlock.SetColor(TintColorId, tintColor);
            renderer.SetPropertyBlock(_propBlock);
        }
    }

    private Image GetAvailableImage()
    {
        foreach (Image candidate in _images)
        {
            if (candidate.Remaining <= 0f)
            {
                return candidate;
            }
        }

        if (_images.Count >= Mathf.Max(1, _maxImages))
        {
            return null;
        }

        var imageObject = new GameObject("Dodge Afterimage");
        imageObject.layer = _source.gameObject.layer;
        // 플레이어가 이동해도 잔상은 생성된 월드 위치에 남는다.
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(imageObject, gameObject.scene);
        var newImage = new Image { Renderer = imageObject.AddComponent<SpriteRenderer>() };
        _images.Add(newImage);
        return newImage;
    }

    private void OnDisable()
    {
        Stop();
        _isGlowActive = false;
        foreach (Image image in _images)
        {
            image.Remaining = 0f;
            if (image.Renderer != null)
            {
                image.Renderer.gameObject.SetActive(false);
            }
        }
    }

    private void OnDestroy()
    {
        foreach (Image image in _images)
        {
            if (image.Renderer != null)
            {
                Destroy(image.Renderer.gameObject);
            }
        }

        _images.Clear();
    }
}