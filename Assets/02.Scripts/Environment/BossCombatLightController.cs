using UnityEngine;
using UnityEngine.Rendering.Universal;

[DefaultExecutionOrder(150), DisallowMultipleComponent]
public sealed class BossCombatLightController : MonoBehaviour
{
    [SerializeField] private Boss _boss;
    [SerializeField, Range(1, 8)] private int _lightCount = 4;
    [SerializeField, Min(0f)] private float _normalIntensity = 0.03f;
    [SerializeField, Min(0f)] private float _heavyIntensity = 0.062f;
    [SerializeField, Min(0f)] private float _parryIntensity = 0.124f;
    [SerializeField, Min(0.01f)] private float _pulseDuration = 0.12f;
    [SerializeField, Min(0.1f)] private float _normalRadius = 2f;
    [SerializeField, Min(0.1f)] private float _heavyRadius = 3f;
    [SerializeField, Min(0.1f)] private float _parryRadius = 4f;
    [SerializeField, Min(0f)] private float _poisonImpactIntensity = 0.06f;
    [SerializeField, Min(0.1f)] private float _poisonImpactRadius = 2.5f;
    private static BossCombatLightController _instance;
    private Light2D[] _lights;
    private float[] _remaining;
    private float[] _peaks;
    private Light2D _poisonLight;
    private BossPoisonBeam _beam;
    private int _nextLight;

    private void Awake()
    {
        int[] layers = { SortingLayer.NameToID("Ground"), SortingLayer.NameToID("Character"), SortingLayer.NameToID("Enemy") };
        _lights = new Light2D[_lightCount];
        _remaining = new float[_lightCount];
        _peaks = new float[_lightCount];
        for (int i = 0; i < _lightCount; i++) _lights[i] = CreateLight("CombatImpactLight", layers);
        _poisonLight = CreateLight("PoisonContactLight", layers);
        _poisonLight.color = new Color(0.65f, 1f, 0.25f);
    }

    private Light2D CreateLight(string objectName, int[] layers)
    {
        var lightObject = new GameObject(objectName);
        lightObject.transform.SetParent(transform, false);
        Light2D light = lightObject.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.blendStyleIndex = 0;
        light.targetSortingLayers = layers;
        light.pointLightInnerRadius = 0f;
        light.overlapOperation = Light2D.OverlapOperation.Additive;
        light.shadowsEnabled = false;
        light.enabled = false;
        return light;
    }

    private void Start()
    {
        if (_boss != null) _beam = _boss.GetComponentInChildren<BossPoisonBeam>(true);
    }

    private void OnEnable()
    {
        _instance = this;
        GameEvents.OnPlayerHit += HandlePlayerHit;
        GameEvents.OnEnemyHit += HandleEnemyHit;
        GameEvents.OnParry += HandleParry;
        GameEvents.OnFinisher += HandleFinisher;
    }

    private void OnDisable()
    {
        if (_instance == this) _instance = null;
        GameEvents.OnPlayerHit -= HandlePlayerHit;
        GameEvents.OnEnemyHit -= HandleEnemyHit;
        GameEvents.OnParry -= HandleParry;
        GameEvents.OnFinisher -= HandleFinisher;
        if (_lights != null)
            for (int i = 0; i < _lights.Length; i++) { _lights[i].enabled = false; _remaining[i] = 0f; }
        if (_poisonLight != null) _poisonLight.enabled = false;
    }

    private void HandlePlayerHit(HitData hit) => PlayPulse(hit.HitPoint, new Color(1f, 0.4f, 0.25f), _normalIntensity, _normalRadius);
    private void HandleEnemyHit(HitData hit) => PlayPulse(hit.HitPoint,
        hit.IsTalisman ? new Color(1f, 0.55f, 0.15f) : new Color(1f, 0.9f, 0.7f),
        hit.IsHeavyImpact ? _heavyIntensity : _normalIntensity, hit.IsHeavyImpact ? _heavyRadius : _normalRadius);
    private void HandleParry(HitData hit) => PlayPulse(hit.HitPoint, new Color(1f, 0.95f, 0.6f), _parryIntensity, _parryRadius);
    private void HandleFinisher(Vector2 point) => PlayPulse(point, new Color(1f, 0.95f, 0.6f), _parryIntensity, _parryRadius);

    public static void PlayStompPulse(Vector2 point)
    {
        if (_instance != null) _instance.PlayPulse(point, new Color(1f, 0.75f, 0.35f), _instance._heavyIntensity, _instance._heavyRadius);
    }

    private void PlayPulse(Vector2 point, Color color, float intensity, float radius)
    {
        if (_lights == null) return;
        int index = _nextLight;
        _nextLight = (_nextLight + 1) % _lights.Length;
        _lights[index].transform.position = new Vector3(point.x, point.y, 0f);
        _lights[index].color = color;
        _lights[index].pointLightOuterRadius = radius;
        _lights[index].intensity = intensity;
        _lights[index].enabled = intensity > 0f;
        _peaks[index] = intensity;
        _remaining[index] = _pulseDuration;
    }

    private void Update()
    {
        for (int i = 0; i < _lights.Length; i++)
        {
            if (_remaining[i] <= 0f) continue;
            _remaining[i] = Mathf.Max(0f, _remaining[i] - Time.unscaledDeltaTime);
            float fade = _remaining[i] / Mathf.Max(_pulseDuration, 0.01f);
            _lights[i].intensity = _peaks[i] * fade * fade;
            _lights[i].enabled = _remaining[i] > 0f;
        }
        Vector2 point = default;
        bool hasImpact = _beam != null && _beam.TryGetTerrainImpact(out point);
        bool isVisible = hasImpact && _poisonImpactIntensity > 0f;
        if (_poisonLight.enabled != isVisible) _poisonLight.enabled = isVisible;
        if (!hasImpact) return;
        var position = new Vector3(point.x, point.y, 0f);
        if (_poisonLight.transform.position != position) _poisonLight.transform.position = position;
        if (_poisonLight.pointLightOuterRadius != _poisonImpactRadius) _poisonLight.pointLightOuterRadius = _poisonImpactRadius;
        if (_poisonLight.intensity != _poisonImpactIntensity) _poisonLight.intensity = _poisonImpactIntensity;
    }
}
