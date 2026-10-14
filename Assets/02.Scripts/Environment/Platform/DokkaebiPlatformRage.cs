using UnityEngine;

/// <summary>발판 위의 적중으로 분노를 채우고, 분노 중에만 게이지를 소모한다.</summary>
[DisallowMultipleComponent]
public sealed class DokkaebiPlatformRage : MonoBehaviour
{
    private const float MinimumDuration = 0.01f;
    private static readonly int IdleState = Animator.StringToHash("Base Layer.Dokkebi_platform_Idle");
    private static readonly int AngryState = Animator.StringToHash("Base Layer.Dokkebi_platform_angry");
    private static readonly int HoldState = Animator.StringToHash("Base Layer.Dokkebi_platform_angry_Idle");
    private static readonly int RageFlag = Animator.StringToHash("IsRaging");
    private static readonly int ReleaseState = Animator.StringToHash("Base Layer.Dokkebi_platform_release");
    private static readonly int StunTrigger = Animator.StringToHash("Stun");
    private static readonly int StunState = Animator.StringToHash("Base Layer.Dokkebi_platform_stun");

    private enum RagePhase { Idle, Heating, Angry, Releasing, Cooling, Stunned }

    [SerializeField] private Animator _animator;
    [SerializeField] private DokkaebiPlatformHeatVisual _heatVisual;
    [SerializeField] private AnimationClip _releaseAnimation;
    [Header("분노게이지")]
    [SerializeField, Min(1f)] private float _maximumRage = 100f;
    [SerializeField, Min(1f)] private float _ragePerHit = 8f;
    [SerializeField, Min(1f)] private float _rageDuration = 8.32f;
    [Header("뜨거운 발판 피해")]
    [SerializeField, Min(1f)] private float _heatDamage = 8f;
    [SerializeField, Min(0.1f)] private float _heatDamageInterval = 1.1f;
    [Header("발판 충돌선과 전투 영역")]
    [SerializeField] private Collider2D _platformSurface;
    [SerializeField, Min(0.1f)] private float _battleHeight = 6f;
    [Header("게이지 위치")]
    [SerializeField] private Vector2 _gaugeOffset = new Vector2(0f, -1.8f);
    [SerializeField, ReadOnly] private float _currentRage;
    [SerializeField, ReadOnly] private float _rageTimeRemaining;
    [SerializeField, ReadOnly] private RagePhase _phase;
    [SerializeField, ReadOnly] private int _rageActivationCount;

    private float _rageEndTime;
    private float _releaseCoolingTime;
    private bool _isConfigured;
    private PlayerHealth _playerHealth;
    private Collider2D _playerBody;
    private float _nextHeatDamageTime;

    public float GaugeRatio => _maximumRage > 0f ? Mathf.Clamp01(_currentRage / _maximumRage) : 0f;
    public int RageActivationCount => _rageActivationCount;
    public bool IsStunned => _phase == RagePhase.Stunned;
    public bool IsRaging => _phase == RagePhase.Heating || _phase == RagePhase.Angry || _phase == RagePhase.Releasing;
    public Vector3 GaugeWorldPosition => transform.TransformPoint(_gaugeOffset);

    private void Awake()
    {
        if (_platformSurface == null) TryGetComponent(out _platformSurface);
        if (_platformSurface == null)
        {
            Debug.LogWarning("DokkaebiPlatformRage: 발판 충돌선 Collider2D가 없습니다.", this);
            enabled = false;
            return;
        }
        if (_animator == null || _heatVisual == null || _releaseAnimation == null ||
            !_animator.HasState(0, IdleState) || !_animator.HasState(0, AngryState) ||
            !_animator.HasState(0, HoldState) || !_animator.HasState(0, ReleaseState))
        {
            Debug.LogWarning("DokkaebiPlatformRage: Animator 상태, 열기 효과 또는 Release 애니메이션 연결이 없습니다.", this);
            enabled = false;
            return;
        }
        bool hasCoolingEvent = false;
        foreach (AnimationEvent animationEvent in _releaseAnimation.events)
        {
            if (animationEvent.functionName != nameof(HandleHeatReleased)) continue;
            _releaseCoolingTime = animationEvent.time;
            hasCoolingEvent = true;
            break;
        }
        if (!hasCoolingEvent || _releaseCoolingTime >= _rageDuration)
        {
            Debug.LogWarning("DokkaebiPlatformRage: Release의 냉각 이벤트가 없거나 분노 시간보다 늦습니다.", this);
            enabled = false;
            return;
        }
        _isConfigured = true;
    }

    private void OnEnable()
    {
        if (!_isConfigured) return;
        ResetRageCycle();
        GameEvents.OnPlayerHit += HandleAttackHit;
        GameEvents.OnEnemyHit += HandleAttackHit;
        GameEvents.OnFinisher += HandleFinisherHit;
    }

    private void Update()
    {
        if (_phase != RagePhase.Angry && _phase != RagePhase.Releasing) return;
        _rageTimeRemaining = Mathf.Max(0f, _rageEndTime - Time.time);
        _currentRage = _maximumRage * Mathf.Clamp01(_rageTimeRemaining / _rageDuration);

        if (_phase == RagePhase.Angry && _rageTimeRemaining <= _releaseCoolingTime)
        {
            _phase = RagePhase.Releasing;
            _animator.SetBool(RageFlag, false);
            // 프레임 지연으로 넘긴 시간만큼 Release를 진행해 6프레임을 종료 시점에 맞춘다.
            float elapsed = Mathf.Max(0f, _releaseCoolingTime - _rageTimeRemaining);
            _animator.Play(ReleaseState, 0, elapsed / Mathf.Max(MinimumDuration, _releaseAnimation.length));
            if (_rageTimeRemaining <= 0f) HandleHeatReleased();
        }
    }

    private void Start()
    {
        var player = GameObject.FindWithTag(Constants.Tags.Player);
        if (player != null)
        {
            _playerHealth = player.GetComponent<PlayerHealth>();
            _playerBody = player.GetComponent<Collider2D>();
        }
        if (_playerHealth == null || _playerBody == null)
        {
            Debug.LogWarning("DokkaebiPlatformRage: 열기 피해 대상의 PlayerHealth 또는 Collider2D가 없습니다.", this);
        }
    }

    private void FixedUpdate()
    {
        if (!_isConfigured || !_heatVisual.IsHot || _playerHealth == null || _playerBody == null ||
            _playerHealth.IsDead || _playerHealth.IsInvincible || Time.time < _nextHeatDamageTime ||
            !_playerBody.IsTouching(_platformSurface))
        {
            return;
        }

        Vector2 hitPoint = _platformSurface.ClosestPoint(_playerBody.bounds.center);
        var hitData = new HitData(_heatDamage, 0f, hitPoint, Vector2.up,
            new AttackDefense { Unblockable = true }, attackName: "뜨거운 도깨비 발판");
        hitData.Attacker = gameObject;
        if (_playerHealth.TakeDamage(hitData))
        {
            _nextHeatDamageTime = Time.time + _heatDamageInterval;
        }
    }

    private void HandleAttackHit(HitData hitData)
    {
        AddRageAtHitPoint(hitData.HitPoint);
    }

    private void HandleFinisherHit(Vector2 hitPoint)
    {
        AddRageAtHitPoint(hitPoint);
    }

    private void AddRageAtHitPoint(Vector2 hitPoint)
    {
        if (_phase != RagePhase.Idle && _phase != RagePhase.Cooling) return;
        Bounds surface = _platformSurface.bounds;
        float battleHeight = Mathf.Max(0.1f, _battleHeight) * Mathf.Abs(transform.lossyScale.y);
        if (hitPoint.x < surface.min.x || hitPoint.x > surface.max.x ||
            hitPoint.y < surface.max.y || hitPoint.y > surface.max.y + battleHeight)
        {
            return;
        }

        _currentRage = Mathf.Min(_maximumRage, _currentRage + _ragePerHit);
        if (_currentRage < _maximumRage || _phase == RagePhase.Cooling) return;
        StartHeating();
    }

    private void StartHeating()
    {
        _phase = RagePhase.Heating;
        _animator.SetBool(RageFlag, true);
        _animator.Play(AngryState, 0, 0f);
    }

    // Angry 13프레임의 Animation Event. 14프레임까지 변색을 완료한다.
    public void HandleRageStarted()
    {
        if (!_isConfigured || _phase != RagePhase.Heating) return;
        _phase = RagePhase.Angry;
        _rageActivationCount++;
        _rageTimeRemaining = _rageDuration;
        _rageEndTime = Time.time + _rageDuration;
        _heatVisual.SetHeated(true);
    }

    // Angry의 마지막 자세를 유지하여 분노 시간 동안 전조 동작을 반복하지 않는다.
    public void HandleHotPoseHeld()
    {
        if (!_isConfigured || _phase != RagePhase.Angry) return;
        _animator.Play(HoldState, 0, 0f);
    }

    // Release 6프레임의 Animation Event. 7프레임까지 원래 색으로 돌아온다.
    public void HandleHeatReleased()
    {
        if (!_isConfigured || _phase != RagePhase.Releasing) return;
        _currentRage = 0f;
        _rageTimeRemaining = 0f;
        _heatVisual.SetHeated(false);
        _phase = RagePhase.Cooling;
        _nextHeatDamageTime = 0f;
    }

    public void HandleReleaseCompleted()
    {
        if (!_isConfigured || _phase != RagePhase.Cooling) return;
        _phase = RagePhase.Idle;
        _animator.Play(IdleState, 0, 0f);
        if (_currentRage >= _maximumRage) StartHeating();
    }

    public bool TryStartStun()
    {
        if (!_isConfigured || IsStunned) return false;
        if (!_animator.HasState(0, StunState))
        {
            Debug.LogWarning("DokkaebiPlatformRage: 도깨비 스턴 Animator 상태가 없습니다.", this);
            return false;
        }
        _phase = RagePhase.Stunned;
        _currentRage = 0f;
        _rageTimeRemaining = 0f;
        _nextHeatDamageTime = 0f;
        _heatVisual.SetHeated(false);
        _animator.SetBool(RageFlag, false);
        _animator.SetTrigger(StunTrigger);
        return true;
    }

    private void ResetRageCycle()
    {
        _rageActivationCount = 0;
        _phase = RagePhase.Idle;
        _currentRage = 0f;
        _rageTimeRemaining = 0f;
        _heatVisual.SetHeated(false);
        _nextHeatDamageTime = 0f;
        _animator.SetBool(RageFlag, false);
        _animator.Play(IdleState, 0, 0f);
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerHit -= HandleAttackHit;
        GameEvents.OnEnemyHit -= HandleAttackHit;
        GameEvents.OnFinisher -= HandleFinisherHit;
        if (_heatVisual != null) _heatVisual.SetHeated(false);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Collider2D surfaceCollider = _platformSurface;
        if (surfaceCollider == null) TryGetComponent(out surfaceCollider);
        if (surfaceCollider == null || !surfaceCollider.enabled) return;
        Bounds surface = surfaceCollider.bounds;
        float battleHeight = Mathf.Max(0.1f, _battleHeight) * Mathf.Abs(transform.lossyScale.y);
        Color previousColor = Gizmos.color;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(new Vector3(surface.center.x, surface.max.y + battleHeight * 0.5f, transform.position.z),
            new Vector3(surface.size.x, battleHeight, 0f));
        Gizmos.color = previousColor;
    }
#endif
}
