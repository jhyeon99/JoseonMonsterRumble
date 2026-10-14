using UnityEngine;

[DisallowMultipleComponent]
public sealed class GamtuPickupVfx : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _hatRenderer;
    [SerializeField] private GameObject _flamePrefab;
    [SerializeField] private AbilityItemData _item;
    [Tooltip("이 아이템의 획득 UI를 닫으면 현재 저장된 장승으로 이동합니다.")]
    [SerializeField] private bool _warpToCheckpointOnClose = true;
    [Header("감투 빛 펄스")]

    [SerializeField] private Vector2 _pulseInterval = new Vector2(2f, 3f);
    [SerializeField, Min(0.1f)] private float _pulseDuration = 0.55f;
    [Header("챙 주변 작은 불꽃")]

    [SerializeField, Range(1, 6)] private int _rimFlameCount = 3;
    [SerializeField, Min(0.01f)] private float _rimFlameSize = 0.2f;
    [SerializeField, Range(0f, 1f)] private float _brimHeight = 0.65f;
    [Header("획득 시 흩어지는 불꽃 4개")]

    [SerializeField, Min(0.1f)] private float _burstDuration = 0.85f;
    [SerializeField, Min(0.1f)] private float _burstDistance = 1.7f;
    [SerializeField, Min(0.01f)] private float _burstSize = 0.65f;

    private static readonly int PulseId = Shader.PropertyToID("_Pulse");
    private static readonly int RectId = Shader.PropertyToID("_SpriteUVRect");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private MaterialPropertyBlock _hatProperties;
    private MaterialPropertyBlock _flameProperties;
    private SpriteRenderer[] _rimFlames;
    private float _nextPulse;
    private float _pulseStart = float.NegativeInfinity;
    private bool _collected;
    private bool _isWaitingForCheckpointWarp;

    private void Awake()
    {
        if (_hatRenderer == null) _hatRenderer = GetComponentInChildren<SpriteRenderer>();
        _hatProperties = new MaterialPropertyBlock();
        _flameProperties = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        ItemAcquisitionView.OnItemAcquisitionClosed += HandleItemAcquisitionClosed;
        _nextPulse = Time.time + Random.Range(_pulseInterval.x, _pulseInterval.y);
        _pulseStart = float.NegativeInfinity;
    }

    private void OnDisable()
    {
        ItemAcquisitionView.OnItemAcquisitionClosed -= HandleItemAcquisitionClosed;
        _isWaitingForCheckpointWarp = false;
    }

    private void HandleItemAcquisitionClosed()
    {
        if (!_isWaitingForCheckpointWarp) return;
        _isWaitingForCheckpointWarp = false;
        if (StageManager.Instance != null)
        {
            StageManager.Instance.TryWarpToActiveCheckpoint();
        }
        else
        {
            Debug.LogWarning($"{name}: 장승 워프에 필요한 StageManager가 없습니다.", this);
        }
        Destroy(gameObject);
    }

    private void Start()
    {
        if (_hatRenderer == null || _flamePrefab == null) return;
        _rimFlames = new SpriteRenderer[_rimFlameCount];
        for (int i = 0; i < _rimFlames.Length; i++)
        {
            GameObject flame = Instantiate(_flamePrefab, transform);
            flame.name = "GamtuBrimFlame_" + i;
            // 캔버스 크기를 월드 기준으로 맞추어 감투의 작은 루트 스케일에 영향을 받지 않는다.
            flame.transform.localScale = WorldScale(_rimFlameSize);
            _rimFlames[i] = flame.GetComponent<SpriteRenderer>();
            if (_rimFlames[i] != null)
            {
                _rimFlames[i].sortingLayerID = _hatRenderer.sortingLayerID;
                _rimFlames[i].sortingOrder = _hatRenderer.sortingOrder + 1;
            }
        }
    }

    private Vector3 WorldScale(float size)
    {
        Vector3 parentScale = transform.lossyScale;
        return new Vector3(size / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)),
            size * 1.2f / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)), 1f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_collected) return;
        if (other.CompareTag(Constants.Tags.Player))
        {
            Player player = other.GetComponentInParent<Player>();
            if (player == null || player.Health == null || player.Health.IsDead) return;

            if (_item != null)
            {
                if (ItemAcquisitionView.Instance != null)
                {
                    if (!ItemAcquisitionView.Instance.TryAcquireItem(_item))
                    {
                        return;
                    }
                    _isWaitingForCheckpointWarp = _warpToCheckpointOnClose;
                }
                else
                {
                    AbilityItemProgress.TryAcquireItem(_item);
                }
            }

            _collected = true;
            OnItemPickedUp();
        }
    }

    private void OnItemPickedUp()
    {
        PlayPickupBurst();

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        if (_hatRenderer != null) _hatRenderer.enabled = false;

        if (_rimFlames != null)
        {
            for (int i = 0; i < _rimFlames.Length; i++)
            {
                if (_rimFlames[i] != null)
                {
                    _rimFlames[i].enabled = false;
                }
            }
        }

        if (!_isWaitingForCheckpointWarp)
        {
            Destroy(gameObject, _burstDuration);
        }
    }

    private void LateUpdate()
    {
        if (_collected || _hatRenderer == null || _hatRenderer.sprite == null) return;
        if (Time.time >= _nextPulse)
        {
            _pulseStart = Time.time;
            _nextPulse = Time.time + Random.Range(_pulseInterval.x, _pulseInterval.y);
        }

        float progress = Mathf.Clamp01((Time.time - _pulseStart) / _pulseDuration);
        float pulse = Mathf.Pow(Mathf.Sin(progress * Mathf.PI), 4f);
        _hatRenderer.GetPropertyBlock(_hatProperties);
        _hatProperties.SetFloat(PulseId, pulse);
        Sprite sprite = _hatRenderer.sprite;
        Rect rect = sprite.textureRect;
        _hatProperties.SetVector(RectId, new Vector4(rect.xMin / sprite.texture.width,
            rect.yMin / sprite.texture.height, rect.xMax / sprite.texture.width, rect.yMax / sprite.texture.height));
        _hatRenderer.SetPropertyBlock(_hatProperties);

        if (_rimFlames == null) return;
        Bounds bounds = _hatRenderer.bounds;
        for (int i = 0; i < _rimFlames.Length; i++)
        {
            SpriteRenderer flame = _rimFlames[i];
            if (flame == null) continue;
            float phase = Time.time * 1.5f + i * 2.4f;
            float across = (i + 0.5f) / _rimFlames.Length;
            flame.transform.position = new Vector3(
                Mathf.Lerp(bounds.min.x, bounds.max.x, across) + Mathf.Sin(phase) * 0.04f,
                bounds.min.y + bounds.size.y * _brimHeight + Mathf.Cos(phase) * 0.055f, bounds.center.z);
            flame.GetPropertyBlock(_flameProperties);
            _flameProperties.SetFloat(IntensityId, 1f + pulse * 1.5f);
            flame.SetPropertyBlock(_flameProperties);
        }
    }

    public void PlayPickupBurst()
    {
        if (_collected) return;
        _collected = true;
        if (_flamePrefab == null) return;
        Vector3 origin = _hatRenderer != null ? _hatRenderer.bounds.center : transform.position;
        for (int i = 0; i < 4; i++)
        {
            float angle = (45f + i * 90f) * Mathf.Deg2Rad;
            GameObject flame = Instantiate(_flamePrefab, origin, Quaternion.identity);
            flame.name = "GamtuPickupFlame_" + i;
            SpriteRenderer renderer = flame.GetComponent<SpriteRenderer>();
            if (renderer != null && _hatRenderer != null)
            {
                renderer.sortingLayerID = _hatRenderer.sortingLayerID;
                renderer.sortingOrder = _hatRenderer.sortingOrder + 2;
            }

            flame.AddComponent<BlueFlameScatter>().Initialize(
                new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), _burstDistance, _burstDuration, _burstSize);
        }
    }
}
