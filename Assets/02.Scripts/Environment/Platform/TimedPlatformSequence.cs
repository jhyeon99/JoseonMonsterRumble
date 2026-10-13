using UnityEngine;

/// <summary>발판 묶음의 등장, 유지, 소멸을 하나의 물리 시간 기준으로 반복한다.</summary>
[DisallowMultipleComponent]
public sealed class TimedPlatformSequence : MonoBehaviour
{
    private const float MinimumDuration = 0.02f;
    private const float BlinkPeriod = 0.12f;

    [System.Serializable]
    private sealed class Platform
    {
        [SerializeField] private SpriteRenderer _artwork;
        [SerializeField] private Collider2D _collider;

        public SpriteRenderer Artwork => _artwork;
        public Collider2D Collider => _collider;
    }

    [Header("등장 순서")]
    [SerializeField] private Platform[] _platforms;
    [Header("반복 시간")]
    [SerializeField, Min(MinimumDuration)] private float _stepInterval = 0.7f;
    [SerializeField, Min(MinimumDuration)] private float _visibleDuration = 1.4f;
    [SerializeField, Min(0f)] private float _appearWarningDuration = 0.25f;
    [SerializeField, Min(0f)] private float _disappearWarningDuration = 0.2f;
    [SerializeField, Min(0f)] private float _restartDelay = 0.7f;
    [SerializeField, Min(0f)] private float _phaseOffset;
    [Header("발판 색상")]
    [SerializeField] private Color _solidColor = Color.white;
    [SerializeField, Range(0f, 1f)] private float _warningAlpha = 0.2f;
    [SerializeField, ReadOnly] private float _cycleTime;

    private float _startTime;
    private float _cycleDuration;
    private bool _isConfigured;

    private void Awake()
    {
        if (_platforms == null || _platforms.Length == 0)
        {
            Debug.LogWarning("TimedPlatformSequence: 발판 목록이 없습니다.", this);
            enabled = false;
            return;
        }
        foreach (Platform platform in _platforms)
        {
            if (platform == null || platform.Artwork == null || platform.Collider == null)
            {
                Debug.LogWarning("TimedPlatformSequence: 발판 그림 또는 Collider 참조가 없습니다.", this);
                enabled = false;
                return;
            }
        }
        _isConfigured = true;
    }

    private void OnEnable()
    {
        if (_isConfigured)
        {
            ResetSequence();
        }
    }

    public void ResetSequence()
    {
        if (!_isConfigured)
        {
            return;
        }
        _startTime = Time.fixedTime;
        _cycleDuration = (_platforms.Length - 1) * _stepInterval +
            _appearWarningDuration + _visibleDuration + _restartDelay;
        _cycleTime = Mathf.Repeat(_phaseOffset, _cycleDuration);
        UpdatePlatforms();
    }

    private void FixedUpdate()
    {
        _cycleTime = Mathf.Repeat(Time.fixedTime - _startTime + _phaseOffset, _cycleDuration);
        UpdatePlatforms();
    }

    private void UpdatePlatforms()
    {
        for (int i = 0; i < _platforms.Length; i++)
        {
            Platform platform = _platforms[i];
            float localTime = _cycleTime - i * _stepInterval;
            float solidTime = localTime - _appearWarningDuration;
            bool isSolid = solidTime >= 0f && solidTime < _visibleDuration;
            bool isVisible = localTime >= 0f && solidTime < _visibleDuration;
            Color color = _solidColor;
            if (!isSolid)
            {
                color.a *= _warningAlpha;
            }
            else if (solidTime >= _visibleDuration - _disappearWarningDuration)
            {
                color.a *= Mathf.Repeat(solidTime, BlinkPeriod) < BlinkPeriod * 0.5f ? 1f : _warningAlpha;
            }
            // 표시와 판정을 같은 물리 갱신에서 전환한다.
            if (platform.Collider.enabled != isSolid)
            {
                platform.Collider.enabled = isSolid;
            }
            platform.Artwork.enabled = isVisible;
            platform.Artwork.color = color;
        }
    }

    private void OnDisable()
    {
        if (_platforms == null)
        {
            return;
        }
        foreach (Platform platform in _platforms)
        {
            if (platform == null) continue;
            if (platform.Collider != null) platform.Collider.enabled = false;
            if (platform.Artwork != null) platform.Artwork.enabled = false;
        }
    }

    private void OnValidate()
    {
        _stepInterval = Mathf.Max(MinimumDuration, _stepInterval);
        _visibleDuration = Mathf.Max(MinimumDuration, _visibleDuration);
        _appearWarningDuration = Mathf.Max(0f, _appearWarningDuration);
        _disappearWarningDuration = Mathf.Clamp(_disappearWarningDuration, 0f, _visibleDuration);
        _restartDelay = Mathf.Max(0f, _restartDelay);
        _phaseOffset = Mathf.Max(0f, _phaseOffset);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_platforms == null) return;
        Color previousColor = Gizmos.color;
        Gizmos.color = Color.cyan;
        SpriteRenderer previous = null;
        for (int i = 0; i < _platforms.Length; i++)
        {
            SpriteRenderer artwork = _platforms[i]?.Artwork;
            if (artwork == null) continue;
            UnityEditor.Handles.Label(artwork.bounds.max, (i + 1).ToString());
            if (previous != null) Gizmos.DrawLine(previous.transform.position, artwork.transform.position);
            previous = artwork;
        }
        Gizmos.color = previousColor;
    }
#endif
}
