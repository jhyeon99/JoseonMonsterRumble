using System;
using UnityEngine;

public enum GuardResult
{
    None,
    Blocked,
    Parried,
}

/// <summary>
/// 플레이어 가드와 패링.
/// - 아래 방향키를 누르고 있는 동안 가드 (PlayerGuardState). 누르고 있는 동안 게이지 소모
/// - 가드를 놓고 일정 시간이 지나면 게이지 회복
/// - 패링: 가드를 시작한 순간부터 패링 판정 시간 안에 패링 가능한 공격이 닿으면 패링 (공격자는 그로기)
/// - 패링이 아니어도 가드 중에 맞으면 막고, 공격마다 정해진 만큼 게이지 소모 (Unblockable 공격 제외)
/// - 남은 게이지가 공격의 소모량보다 적으면 가드 브레이크 (막지 못하고 그대로 피격)
/// 공격별 규칙(패링 가능, 가드 불가, 판정 시간, 게이지 소모)은 각 공격의 AttackDefense에서 조정
/// </summary>
public class PlayerGuard : MonoBehaviour
{
    [Header("가드 게이지")]
    [SerializeField, Min(1f)] private float _maxGauge = 100f;

    [Tooltip("가드를 누르고 있는 동안 초당 소모량. 0이면 막을 때만 소모 (공격별 GuardGaugeCost)")]
    [SerializeField, Min(0f)]
    private float _drainPerSecond = 30f;

    [Tooltip("일반 가드 피격 후 지속 소모를 멈추는 시간")]
    [SerializeField, Min(0f)]
    private float _postBlockDrainPauseDuration = 0.3f;

    [Tooltip("지속 소모 정지를 적용할 최소 피격 게이지 감소 비율")]
    [SerializeField, Range(0f, 1f)]
    private float _postBlockDrainPauseMinimumRatio = 0.1f;

    [Tooltip("가드를 쓰지 않을 때 초당 회복량")]
    [SerializeField, Min(0f)]
    private float _recoverPerSecond = 40f;

    [Tooltip("가드를 놓은 뒤 회복이 시작될 때까지 대기 시간 (초)")]
    [SerializeField, Min(0f)]
    private float _recoverDelay = 0.5f;

    [Tooltip("이 값 이상 남아 있어야 가드를 시작할 수 있음 (게이지가 바닥난 직후 가드가 켜졌다 꺼졌다 하는 것 방지)")]
    [SerializeField, Min(0f)]
    private float _minGaugeToStart = 10f;

    [Header("가드 판정")]
    [Tooltip("체크하면 바라보는 방향에서 온 공격만 막음 (등 뒤 공격은 그대로 맞음)")]
    [SerializeField]
    private bool _blockOnlyFront = false;

    [Header("가드 넉백")]
    [Tooltip("일반 가드 성공 시 공격 진행 방향으로 밀리는 초기 수평 속도")]
    [SerializeField, Min(0f)]
    private float _blockKnockbackSpeed = 5f;

    [Tooltip("일반 가드 넉백의 수평 감속도")]
    [SerializeField, Min(0.1f)]
    private float _blockKnockbackDeceleration = 50f;

    [Header("패링")]
    [Tooltip("가드를 시작한 순간부터 패링으로 인정하는 기본 시간 (초). 공격별 ParryWindowOverride가 있으면 그 값 사용")]
    [SerializeField, Min(0f)]
    private float _parryWindow = 0.18f;

    [Tooltip("패링 판정을 연 뒤 다시 열 수 있을 때까지 시간 (초). 아래 방향키 연타로 패링 판정을 계속 여는 것 방지")]
    [SerializeField, Min(0f)]
    private float _parryCooldown = 0.5f;

    [Header("패링 반사 (투사체)")]
    [Tooltip("패링으로 튕겨 낸 투사체의 속도 (초당 거리). 원래 투사체 속도와 관계없이 이 값 사용")]
    [SerializeField, Min(0.1f)]
    private float _reflectSpeed = 15f;

    [Tooltip("튕겨 낸 투사체의 데미지 배율 (원래 데미지 기준)")]
    [SerializeField, Min(0f)]
    private float _reflectDamageMultiplier = 2f;

    [Header("디버그")]
    [SerializeField] private bool _logResult = true;

    [Header("상태 (확인용)")]
    [SerializeField, ReadOnly] private float _currentGauge;
    [SerializeField, ReadOnly] private bool _isGuarding;

    private float _recoverTimer;
    private float _drainResumeTime = float.NegativeInfinity;

    // 이번 가드의 패링 판정 시작 시각 (판정이 없으면 -Infinity)
    private float _parryWindowStartTime = float.NegativeInfinity;

    // 마지막으로 패링 판정을 연 시각 (연타 방지용)
    private float _lastParryWindowOpenedTime = float.NegativeInfinity;

    public float CurrentGauge => _currentGauge;
    public float MaxGauge => _maxGauge;
    public bool IsGuarding => _isGuarding;
    public bool IsGaugeEmpty => _currentGauge <= 0f;
    public bool CanStartGuard => _currentGauge >= _minGaugeToStart;
    public float ReflectSpeed => _reflectSpeed;
    public float ReflectDamageMultiplier => _reflectDamageMultiplier;
    public float BlockKnockbackSpeed => _blockKnockbackSpeed;
    public float BlockKnockbackDeceleration => _blockKnockbackDeceleration;
    public float BlockDrainPauseRemaining => Mathf.Max(0f, _drainResumeTime - Time.time);

    /// <summary>기본 판정 시간 기준으로 지금 패링 판정 중인지 (UI, 디버그용)</summary>
    public bool IsParryWindowOpen => _isGuarding && Time.time - _parryWindowStartTime <= _parryWindow;

    /// <summary>게이지 변경 (현재값, 최대값). UI 연결용</summary>
    public event Action<float, float> OnGaugeChanged;

    /// <summary>가드로 막았을 때 (패링 제외)</summary>
    public event Action<HitData> OnBlocked;

    /// <summary>패링 성공</summary>
    public event Action<HitData> OnParried;

    /// <summary>가드가 깨졌을 때 (게이지 부족으로 피격되거나 막다가 게이지가 바닥남). 가드 브레이크 연출용</summary>
    public event Action OnGuardBroken;

    private void Awake()
    {
        _currentGauge = _maxGauge;
    }

    private void Start()
    {
        OnGaugeChanged?.Invoke(_currentGauge, _maxGauge);
    }

    private void OnDisable()
    {
        _isGuarding = false;
        _parryWindowStartTime = float.NegativeInfinity;
        _drainResumeTime = float.NegativeInfinity;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        if (_isGuarding)
        {
            if (BlockDrainPauseRemaining > 0f)
            {
                return;
            }

            ChangeGauge(-_drainPerSecond * deltaTime);
            return;
        }

        if (_recoverTimer > 0f)
        {
            _recoverTimer -= deltaTime;
            return;
        }

        if (_currentGauge < _maxGauge)
        {
            ChangeGauge(_recoverPerSecond * deltaTime);
        }
    }

    /// <param name="canOpenParryWindow">
    /// 아래 방향키를 방금 눌러서 가드를 시작했으면 true.
    /// 공격 중 등 미리 누르고 있다가 가드로 넘어온 경우는 패링 판정 없이 가드만 함
    /// </param>
    public void BeginGuard(bool canOpenParryWindow)
    {
        _isGuarding = true;
        _parryWindowStartTime = float.NegativeInfinity;

        if (!canOpenParryWindow)
        {
            return;
        }

        if (Time.time - _lastParryWindowOpenedTime < _parryCooldown)
        {
            Log("패링 쿨타임 중 (가드만)");
            return;
        }

        _parryWindowStartTime = Time.time;
        _lastParryWindowOpenedTime = Time.time;
    }

    public void EndGuard()
    {
        if (!_isGuarding)
        {
            return;
        }

        _isGuarding = false;
        _parryWindowStartTime = float.NegativeInfinity;
        _recoverTimer = _recoverDelay;
    }

    /// <summary>
    /// 피격 시 PlayerHealth가 호출. None이면 평소처럼 피격 처리.
    /// </summary>
    public GuardResult TryHandleHit(HitData hitData)
    {
        if (!_isGuarding)
        {
            return GuardResult.None;
        }

        AttackDefense defense = hitData.Defense;

        if (defense.Unblockable)
        {
            Log("가드 불가 공격 → 피격");
            return GuardResult.None;
        }

        if (_blockOnlyFront && !IsFromFront(hitData))
        {
            Log("등 뒤 공격 → 피격");
            return GuardResult.None;
        }

        if (defense.Parryable && IsInParryWindow(defense))
        {
            // 한 번의 가드 시작으로 한 번만 패링
            _parryWindowStartTime = float.NegativeInfinity;

            hitData.Source?.OnParried(hitData, transform);
            GameEvents.TriggerParry(hitData);
            OnParried?.Invoke(hitData);
            Log("패링 성공");
            return GuardResult.Parried;
        }

        // 남은 게이지가 소모량보다 적으면 가드 브레이크 → 막지 못하고 그대로 피격
        if (defense.GuardGaugeCost > _currentGauge)
        {
            ChangeGauge(-_currentGauge);
            OnGuardBroken?.Invoke();
            Log($"가드 브레이크 (필요 {defense.GuardGaugeCost}, 남은 게이지 부족) → 피격");
            return GuardResult.None;
        }

        if (defense.GuardGaugeCost > 0f)
        {
            ChangeGauge(-defense.GuardGaugeCost);
        }

        StartPostBlockDrainPause(defense.GuardGaugeCost);
        OnBlocked?.Invoke(hitData);
        Log(defense.Parryable ? "가드 (패링 타이밍 아님)" : "가드");

        if (IsGaugeEmpty)
        {
            OnGuardBroken?.Invoke();
            Log("가드 게이지 소진");
        }

        return GuardResult.Blocked;
    }

    private bool IsInParryWindow(AttackDefense defense)
    {
        float window = defense.ParryWindowOverride > 0f ? defense.ParryWindowOverride : _parryWindow;
        return Time.time - _parryWindowStartTime <= window;
    }

    private void StartPostBlockDrainPause(float guardGaugeCost)
    {
        if (_postBlockDrainPauseDuration <= 0f || _maxGauge <= 0f)
        {
            return;
        }

        float gaugeLossRatio = guardGaugeCost / _maxGauge;
        if (gaugeLossRatio < _postBlockDrainPauseMinimumRatio)
        {
            return;
        }

        _drainResumeTime = Time.time + _postBlockDrainPauseDuration;
    }

    // AttackDirection은 공격자 → 플레이어 방향. 바라보는 방향과 반대면 정면에서 온 공격
    private bool IsFromFront(HitData hitData)
    {
        float facing = transform.localScale.x >= 0f ? 1f : -1f;
        return hitData.AttackDirection.x * facing <= 0f;
    }

    private void ChangeGauge(float delta)
    {
        float next = Mathf.Clamp(_currentGauge + delta, 0f, _maxGauge);
        if (Mathf.Approximately(next, _currentGauge))
        {
            return;
        }

        _currentGauge = next;
        OnGaugeChanged?.Invoke(_currentGauge, _maxGauge);
    }

    private void Log(string message)
    {
        if (_logResult)
        {
            Debug.Log($"[PlayerGuard] {message}", this);
        }
    }
}
