using UnityEngine;

/// <summary>도깨비의 Angry 상태에 맞춰 붉은 변색과 상승하는 열기를 표시한다.</summary>
[DisallowMultipleComponent]
public sealed class DokkaebiPlatformHeatVisual : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _artwork;
    [SerializeField] private Material _heatMaterial;
    [Header("변색")]
    [SerializeField] private Color _hotColor = new Color(1f, 0.4f, 0.25f, 1f);
    [SerializeField, Min(0.01f)] private float _colorChangeDuration = 0.2f;
    [Header("열기 발생 위치")]
    [SerializeField] private Vector2 _surfaceOffset = new Vector2(0f, 1.65f);
    [SerializeField, Min(0.1f)] private float _surfaceWidth = 12f;
    [SerializeField, Min(0f)] private float _heatEmissionRate = 24f;
    [SerializeField, ReadOnly] private bool _isHot;

    private Color _restColor;
    private float _heatStrength;
    private ParticleSystem _heatParticles;
    private bool _isConfigured;

    public bool IsHot => _isHot;

    private void Awake()
    {
        if (_artwork == null || _heatMaterial == null)
        {
            Debug.LogWarning("DokkaebiPlatformHeatVisual: 그림 또는 열기 Material이 없습니다.", this);
            enabled = false;
            return;
        }

        _restColor = _artwork.color;
        CreateHeatParticles();
        _isConfigured = true;
    }

    private void LateUpdate()
    {
        if (!_isConfigured) return;

        _heatStrength = Mathf.MoveTowards(_heatStrength, _isHot ? 1f : 0f,
            Time.deltaTime / _colorChangeDuration);
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 8f);
        var color = Color.Lerp(_restColor, _hotColor, _heatStrength * pulse);
        color.a = _restColor.a;
        _artwork.color = color;
    }

    public void SetHeated(bool isHot)
    {
        if (!_isConfigured || _isHot == isHot) return;
        _isHot = isHot;
        if (_isHot) _heatParticles.Play();
        else _heatParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void CreateHeatParticles()
    {
        var heatObject = new GameObject("VFX_PlatformHeat");
        heatObject.transform.SetParent(transform, false);
        heatObject.transform.localPosition = _surfaceOffset;
        _heatParticles = heatObject.AddComponent<ParticleSystem>();
        _heatParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = _heatParticles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 96;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
        main.startSpeed = 0f;
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(0.15f, 0.32f);
        main.startSizeY = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
        main.startSizeZ = 1f;
        main.startColor = Color.white;
        main.gravityModifier = 0f;

        ParticleSystem.EmissionModule emission = _heatParticles.emission;
        emission.rateOverTime = _heatEmissionRate;
        ParticleSystem.ShapeModule shape = _heatParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(_surfaceWidth, 0.08f, 0.01f);

        ParticleSystem.VelocityOverLifetimeModule velocity = _heatParticles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        ParticleSystem.NoiseModule noise = _heatParticles.noise;
        noise.enabled = true;
        noise.separateAxes = true;
        noise.strengthX = 0.12f;
        noise.strengthY = 0.04f;
        noise.strengthZ = 0f;
        noise.frequency = 1.8f;
        noise.scrollSpeed = 1.4f;

        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.45f, 0.15f), 0f),
                new GradientColorKey(new Color(1f, 0.7f, 0.4f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.5f, 0.15f),
                new GradientAlphaKey(0f, 1f) });
        ParticleSystem.ColorOverLifetimeModule color = _heatParticles.colorOverLifetime;
        color.enabled = true;
        color.color = gradient;
        ParticleSystem.SizeOverLifetimeModule size = _heatParticles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.5f));

        ParticleSystemRenderer particleRenderer = heatObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = _heatMaterial;
        particleRenderer.sortingLayerID = _artwork.sortingLayerID;
        particleRenderer.sortingOrder = _artwork.sortingOrder + 1;
    }

    private void OnDisable()
    {
        if (!_isConfigured) return;
        _isHot = false;
        _heatStrength = 0f;
        if (_artwork != null) _artwork.color = _restColor;
        if (_heatParticles != null)
            _heatParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
