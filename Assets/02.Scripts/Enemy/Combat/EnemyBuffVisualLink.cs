using UnityEngine;

[DisallowMultipleComponent]
public class EnemyBuffVisualLink : MonoBehaviour
{
    [Header("에너지 링크 설정")]
    [SerializeField] private LineRenderer _lineRenderer;
    [SerializeField] private int _pointCount = 12;
    [SerializeField] private float _waveAmplitude = 0.2f;
    [SerializeField] private float _waveFrequency = 10f;
    [SerializeField] private float _waveSpeed = 8f;
    [SerializeField] private Color _startColor = new Color(0.2f, 0.8f, 1f, 0.9f);
    [SerializeField] private Color _endColor = new Color(0.1f, 0.5f, 1f, 0.8f);
    [SerializeField] private float _startWidth = 0.15f;
    [SerializeField] private float _endWidth = 0.08f;

    [Header("수혜 몬스터 오라 펄스 설정")]
    [SerializeField] private Color _buffAuraColor = new Color(0.35f, 0.85f, 1f, 1f);
    [SerializeField] private float _pulseSpeed = 6f;
    [SerializeField] private float _pulseIntensity = 0.35f;

    [SerializeField] private SpriteRenderer _selfRenderer;

    private Enemy _currentTarget;
    private SpriteRenderer _targetRenderer;
    private Color _originalTargetColor = Color.white;
    private bool _hasTargetColor;
    private float _waveTimer;

    private void Awake()
    {
        InitializeLineRenderer();
        if (_selfRenderer == null)
        {
            _selfRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

    private void InitializeLineRenderer()
    {
        if (_lineRenderer == null)
        {
            _lineRenderer = GetComponent<LineRenderer>();
            if (_lineRenderer == null)
            {
                _lineRenderer = gameObject.AddComponent<LineRenderer>();
            }
        }

        _lineRenderer.positionCount = _pointCount;
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.startWidth = _startWidth;
        _lineRenderer.endWidth = _endWidth;
        _lineRenderer.startColor = _startColor;
        _lineRenderer.endColor = _endColor;
        _lineRenderer.sortingLayerName = "Enemy";
        _lineRenderer.sortingOrder = 24;

        if (_lineRenderer.sharedMaterial == null)
        {
            var defaultSpriteMat = new Material(Shader.Find("Sprites/Default"));
            _lineRenderer.material = defaultSpriteMat;
        }

        _lineRenderer.enabled = false;
    }

    private static readonly int FlameColorPropId = Shader.PropertyToID("_FlameColor");
    private static readonly int FlameInnerColorPropId = Shader.PropertyToID("_FlameInnerColor");
    private MaterialPropertyBlock _propBlock;

    public void ApplyBuffData(SO_BuffTypeData data)
    {
        if (data == null)
        {
            return;
        }

        if (_selfRenderer == null)
        {
            _selfRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (_selfRenderer != null)
        {
            _selfRenderer.color = Color.white;
            if (_propBlock == null)
            {
                _propBlock = new MaterialPropertyBlock();
            }

            _selfRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(FlameColorPropId, data.FlameColor);
            _propBlock.SetColor(FlameInnerColorPropId, data.FlameInnerColor);
            _selfRenderer.SetPropertyBlock(_propBlock);
        }

        _startColor = data.LinkStartColor;
        _endColor = data.LinkEndColor;
        _buffAuraColor = data.TargetAuraColor;

        if (_lineRenderer != null)
        {
            _lineRenderer.startColor = _startColor;
            _lineRenderer.endColor = _endColor;
        }
    }

    public void StartLink(Enemy target, SO_BuffTypeData buffData = null)
    {
        if (target == null)
        {
            return;
        }

        StopLink();

        if (buffData != null)
        {
            ApplyBuffData(buffData);
        }

        _currentTarget = target;
        _targetRenderer = _currentTarget.GetComponentInChildren<SpriteRenderer>();
        if (_targetRenderer != null)
        {
            _originalTargetColor = _targetRenderer.color;
            _hasTargetColor = true;
        }

        _waveTimer = 0f;
        if (_lineRenderer != null)
        {
            _lineRenderer.enabled = true;
        }
    }

    public void StopLink()
    {
        if (_hasTargetColor && _targetRenderer != null)
        {
            _targetRenderer.color = _originalTargetColor;
        }

        _currentTarget = null;
        _targetRenderer = null;
        _hasTargetColor = false;

        if (_lineRenderer != null)
        {
            _lineRenderer.enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (_currentTarget == null || !_lineRenderer.enabled)
        {
            return;
        }

        if (!_currentTarget.isActiveAndEnabled || _currentTarget.IsDefeated)
        {
            StopLink();
            return;
        }

        _waveTimer += Time.deltaTime;

        UpdateWaveLine();
        UpdateTargetAuraPulse();
    }

    private void UpdateWaveLine()
    {
        Vector2 startPos = transform.position;
        Vector2 endPos = _currentTarget.GetEnemyCenterPosition();
        Vector2 dir = endPos - startPos;
        float distance = dir.magnitude;

        if (distance < 0.05f)
        {
            return;
        }

        Vector2 normal = new Vector2(-dir.y, dir.x).normalized;

        for (int i = 0; i < _pointCount; i++)
        {
            float t = (float)i / (_pointCount - 1);
            var basePos = Vector2.Lerp(startPos, endPos, t);

            float envelope = Mathf.Sin(t * Mathf.PI);
            float wave = Mathf.Sin((_waveTimer * _waveSpeed) + (t * _waveFrequency)) * _waveAmplitude * envelope;

            Vector3 finalPos = basePos + (normal * wave);
            _lineRenderer.SetPosition(i, finalPos);
        }
    }

    private void UpdateTargetAuraPulse()
    {
        if (!_hasTargetColor || _targetRenderer == null)
        {
            return;
        }

        float pulse = (Mathf.Sin(Time.time * _pulseSpeed) + 1f) * 0.5f;
        var blendedColor = Color.Lerp(_originalTargetColor, _buffAuraColor, pulse * _pulseIntensity);
        _targetRenderer.color = blendedColor;
    }

    private void OnDisable()
    {
        StopLink();
    }

    private void OnDestroy()
    {
        StopLink();
    }
}
