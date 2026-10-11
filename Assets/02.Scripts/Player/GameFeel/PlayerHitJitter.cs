using UnityEngine;

/// <summary>
/// 플레이어 피격 시 히트스톱 정지 구간 동안 스프라이트를 실제 시간 기준으로 고주파 미세 진동시키는 컴포넌트.
/// - 애니메이터의 좌표 덮어쓰기를 방지하기 위해 LateUpdate에서 오프셋을 적용
/// - GameEvents.OnPlayerHit 이벤트에 반응하여 독립 동작
/// </summary>
[DisallowMultipleComponent]
public class PlayerHitJitter : MonoBehaviour
{
    [Header("타겟 스프라이트")]
    [Tooltip("진동시킬 대상 스프라이트 렌더러. 미할당 시 플레이어에서 자동 탐색")]
    [SerializeField] private SpriteRenderer _targetRenderer;

    [Header("진동 설정")]
    [Tooltip("진동 지속 시간 (초, 실제 시간 기준)")]
    [SerializeField, Min(0.01f)] private float _duration = 0.14f;

    [Tooltip("좌우 진동 세기 (로컬 X 오프셋)")]
    [SerializeField, Range(0.01f, 0.4f)] private float _intensity = 0.12f;

    [Tooltip("초당 진동 횟수 (주파수)")]
    [SerializeField, Range(10f, 60f)] private float _frequency = 45f;

    private Transform _targetTransform;
    private Vector3 _baseLocalPosition;
    private bool _hasTarget;
    private bool _isJittering;
    private float _elapsed;

    private void Awake()
    {
        InitializeTarget();
    }

    private void OnEnable()
    {
        GameEvents.OnPlayerHit += HandlePlayerHit;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerHit -= HandlePlayerHit;
        StopJitter();
    }

    public void InitializeTarget()
    {
        if (_targetRenderer == null)
        {
            _targetRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (_targetRenderer == null)
        {
            Player player = FindAnyObjectByType<Player>();
            if (player != null)
            {
                _targetRenderer = player.GetComponentInChildren<SpriteRenderer>();
            }
        }

        if (_targetRenderer != null)
        {
            _targetTransform = _targetRenderer.transform;
            _baseLocalPosition = _targetTransform.localPosition;
            _hasTarget = true;
        }
    }

    public void HandlePlayerHit(HitData hitData)
    {
        if (!_hasTarget || _targetTransform == null)
        {
            InitializeTarget();
            if (!_hasTarget || _targetTransform == null)
            {
                return;
            }
        }

        PlayJitter();
    }

    public void PlayJitter()
    {
        if (!_hasTarget || _targetTransform == null)
        {
            return;
        }

        _isJittering = true;
        _elapsed = 0f;
    }

    public void StopJitter()
    {
        if (_isJittering)
        {
            _isJittering = false;
            RestorePosition();
        }
    }

    private void LateUpdate()
    {
        if (!_isJittering)
        {
            return;
        }

        _elapsed += Time.unscaledDeltaTime;

        if (_elapsed >= _duration)
        {
            StopJitter();
            return;
        }

        if (_targetTransform == null)
        {
            _isJittering = false;
            return;
        }

        float decay = 1f - (_elapsed / _duration);
        float wave = Mathf.Sin(_elapsed * _frequency * Mathf.PI * 2f);
        float offsetX = wave * _intensity * decay;

        _targetTransform.localPosition = _baseLocalPosition + new Vector3(offsetX, 0f, 0f);
    }

    private void RestorePosition()
    {
        if (_targetTransform != null)
        {
            _targetTransform.localPosition = _baseLocalPosition;
        }
    }
}
