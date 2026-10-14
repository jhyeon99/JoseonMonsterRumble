using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 소환 궁극기 (체력 비율 발동). 현재 페이즈 체력이 지정한 비율 이하가 되면 다른 패턴보다 먼저 무조건 사용.
/// - 소환 위치(Summon Point)를 지정하면 먼저 그 위치로 점프한 뒤 ultimate 트리거로 Angry → UltimateCharge 시작
/// - 방울이 일정 간격으로 맵 아래에서 올라오고, 터트리면 올챙이가 나옴. 올챙이를 날려 보스에게 맞히면 데미지 없이 횟수만 셈
/// - 제한 시간(Max Duration) 안에 Required Hits만큼 맞히면 파훼: Boss_Hit 애니메이션 후 복귀 패턴(찍기)
/// - 못 맞히면 ultimateRelease + 즉사 연출 + 붉은 화면 후 플레이어 즉사
/// - 차지 애니메이션은 애니메이터 Float(ultimateProgress)로 진행되며, 제한 시간이 끝날 때 1이 되도록 맞춤
/// - 패턴 중 보스는 제자리에 있고, 직접 공격은 받지 않음. 끝나면(끊겨도) 방울 / 올챙이 모두 제거 후 복귀 패턴
///   (판정 시작 / 끝 비율, Animation Trigger는 사용하지 않음. 시작 트리거는 Ultimate Trigger)
/// </summary>
public class BossSummonPattern : BossPattern
{
    [Header("발동 체력 (현재 페이즈 최대 체력 대비 0 ~ 1)")]
    [Tooltip("1페이즈 체력이 이 비율 이하가 되면 사용 (예: 0.5). 비우면 1페이즈에는 사용 안 함")]
    [SerializeField]
    private float[] _phase1HpRatios = new float[0];

    [Tooltip("2페이즈 체력이 이 비율 이하가 되면 사용 (예: 0.7, 0.4)")]
    [SerializeField]
    private float[] _phase2HpRatios = { 0.7f, 0.4f };

    [Header("소환 위치로 점프 (비우면 제자리에서 소환)")]
    [Tooltip("소환할 때 보스가 점프해서 올라갈 고정 위치 (보스 발밑 기준)")]
    [SerializeField]
    private Transform _summonPoint;

    [Tooltip("뛰어오를 때 발밑 먼지 (크기 / 위치는 Boss의 점프 먼지 설정)")]
    [SerializeField]
    private bool _takeoffDust = true;

    [Tooltip("착지할 때 발밑 먼지")]
    [SerializeField]
    private bool _landingDust = true;

    [Tooltip("소환 위치까지 공중에서 이동하는 속도 (초당 거리, 출발 → 도착 직선 거리 기준). 멀수록 오래 떠 있음. " +
             "애니메이션은 시작(Boss_Move_Start) → 공중(체공 시간 동안 유지) → 착지(Boss_Move_End). 상태 이름은 Boss에서 설정")]
    [SerializeField, Min(0.1f)]
    private float _jumpSpeed = 20f;

    [Tooltip("체공 시간 최소 (초)")]
    [SerializeField, Min(0.05f)]
    private float _minJumpDuration = 0.2f;

    [Tooltip("체공 시간 최대 (초)")]
    [SerializeField, Min(0.05f)]
    private float _maxJumpDuration = 1f;

    [Tooltip("출발 / 도착 중 높은 쪽 기준으로 더 올라가는 높이")]
    [SerializeField, Min(0f)]
    private float _jumpHeight = 3f;

    [Tooltip("점프하는 동안 몸통 충돌(플레이어 밀어내기)과 접촉 데미지를 끔")]
    [SerializeField]
    private bool _disableCollisionWhileJumping = true;

    [Tooltip("착지 애니메이션이 끝난 뒤 궁극기 시작까지 추가 대기 (초)")]
    [SerializeField, Min(0f)]
    private float _delayAfterLanding = 0.2f;

    [SerializeField]
    private CameraShakeSettings _landingShake = new CameraShakeSettings(0.2f, 0.2f);

    [Header("복귀")]
    [Tooltip("소환이 끝나면(끊겨도) 바로 사용할 패턴. 찍기 패턴을 넣으면 소환 위치에서 플레이어 쪽으로 찍으면서 내려옴")]
    [SerializeField]
    private BossPattern _returnPattern;

    [Header("독 방울")]
    [SerializeField]
    private BossPoisonBubble _bubblePrefab;

    [Tooltip("한 번에 올라오는 방울 수")]
    [SerializeField, Min(1)]
    private int _bubblesPerWave = 2;

    [Tooltip("방울이 올라오는 간격 (초)")]
    [SerializeField, Min(0.1f)]
    private float _waveInterval = 3f;

    [Tooltip("방울이 생기는 높이 (맵 아래, 화면 밖). 비우면 1층 바닥에서 아래 오프셋만큼")]
    [SerializeField]
    private Transform _spawnLine;

    [Tooltip("생성 높이 기준점이 없을 때 1층 바닥 아래로 내리는 거리")]
    [SerializeField, Min(0f)]
    private float _spawnBelowFloor = 2f;

    [Tooltip("방울이 사라지는 높이 (맵 위 끝). 비우면 방울의 Fallback Life Time 뒤 사라짐")]
    [SerializeField]
    private Transform _despawnLine;

    [Tooltip("맵 좌우 끝에서 이만큼 안쪽에서만 생성")]
    [SerializeField, Min(0f)]
    private float _spawnXMargin = 1f;

    [Tooltip("같은 웨이브 방울끼리 최소 가로 간격")]
    [SerializeField, Min(0f)]
    private float _minSpacing = 2f;

    [Tooltip("궁극기 시작 후 첫 방울이 올라오기까지 (초)")]
    [SerializeField, Min(0f)]
    private float _spawnDelayAfterLoop = 0f;

    [Header("제한 시간")]
    [Tooltip("파훼 제한 시간 (초, 궁극기 애니메이션 시작부터). 차지 애니메이션이 이 시간에 맞춰 끝남")]
    [SerializeField, Min(0.1f)]
    private float _maxDuration = 25f;

    [Header("알 주기 흡입")]
    [SerializeField, Min(0.1f)] private float _suctionInterval = 3f;
    [SerializeField, Min(0f)] private float _suctionDistance = 4.5f;
    [SerializeField, Min(0.01f)] private float _suctionDuration = 0.4f;
    [SerializeField, Min(0.01f)] private float _absorbRadius = 0.8f;
    [SerializeField, Min(0.01f)] private float _absorbDuration = 0.25f;
    [SerializeField, Min(0f)] private float _absorbedTimeReduction = 1f;
    [SerializeField, Min(0f)] private float _bossSpawnExclusionMargin = 1f;

    private readonly List<BossPoisonBubble> _activeBubbles = new List<BossPoisonBubble>();
    private Collider2D _spawnExclusionCollider;
    private float _gimmickDeadline;
    private Animator _chargeAnimator;
    private int _chargeProgressHash;
    private bool _hasChargeProgressParameter;
    private float[] _placedBubbleXs;
    private const int MaxSpawnAttempts = 32;

    [Header("궁극기 기믹 (차지 → 파훼 / 즉사)")]
    [Tooltip("궁극기 시작 애니메이터 트리거 (인스펙터의 Animation Trigger 대신 사용)")]
    [SerializeField]
    private string _ultimateTrigger = "ultimate";

    [Tooltip("파훼에 필요한 올챙이 명중 횟수")]
    [SerializeField, Min(1)]
    private int _requiredHits = 3;

    [Tooltip("차지 애니메이션 클립 (boss_Ultimate_Charge). 이 클립이 재생되기 시작하면 차지 진행도를 0 → 1로 올림. 비우면 패턴 시작부터 진행")]
    [SerializeField]
    private AnimationClip _chargeClip;

    [Tooltip("차지 애니메이터 상태 이름. 이 상태에 들어간 순간부터 차지 진행도를 0 → 1로 올림 (Charge Clip보다 우선)")]
    [SerializeField]
    private string _chargeStateName = "boss_Ultimate_Charge";

    [Tooltip("차지 진행도를 넣을 애니메이터 Float (차지 상태의 Motion Time)")]
    [SerializeField]
    private string _chargeProgressParameter = "ultimateProgress";

    [Tooltip("해방 상태를 직접 재생하지 못했을 때만 보내는 예비 트리거 (차지 → 해방 전이)")]
    [SerializeField]
    private string _releaseTrigger = "ultimateRelease";

    [Tooltip("해방 애니메이터 상태 이름. 실패 시 이 상태를 직접 재생 (트리거 전이에 의존하지 않음)")]
    [SerializeField]
    private string _releaseStateName = "boss_Charge_Release";

    [Tooltip("파훼 시 재생할 피격 애니메이터 상태 이름")]
    [SerializeField]
    private string _hitStateName = "Boss_Hit";

    [Tooltip("피격 애니메이션 클립 (길이만큼 기다린 뒤 복귀 패턴). 비우면 Hit Fallback Duration")]
    [SerializeField]
    private AnimationClip _hitClip;

    [SerializeField, Min(0f)]
    private float _hitFallbackDuration = 1f;

    [Header("차지 중 빨아들이기 VFX")]
    [Tooltip("차지 애니메이션(Ultimate Charge)이 재생되는 동안 보스에 붙여 반복 재생할 VFX (PF_BossSuction_VFX). 비우면 사용 안 함")]
    [SerializeField]
    private GameObject _suctionVfxPrefab;

    [Tooltip("VFX를 붙일 입 위치 (Visual 아래 SuctionPoint. 차지 애니메이션 키로 입을 따라 움직임). " +
             "비우면 보스 아래에서 이름이 SuctionPoint인 오브젝트를 찾고, 없으면 보스에 붙임")]
    [SerializeField]
    private Transform _suctionVfxPoint;

    [Tooltip("붙인 위치 기준 추가 보정 (입 위치에서 살짝 앞 / 위로 옮기고 싶을 때)")]
    [SerializeField]
    private Vector2 _suctionVfxOffset = Vector2.zero;

    [Tooltip("VFX 좌우 반전 (입 방향과 반대로 나오면 켜기)")]
    [SerializeField]
    private bool _flipSuctionVfx = false;

    [Tooltip("VFX 크기 배율")]
    [SerializeField, Min(0.01f)]
    private float _suctionVfxScale = 1f;

    [Header("궁극기 실패 (즉사)")]
    [Tooltip("해방 애니메이션 최대 대기 시간 (초). 보통은 애니메이터가 해방 상태를 벗어나는 순간 끝남")]
    [SerializeField, Min(0f)]
    private float _releaseFallbackDuration = 4f;

    [Tooltip("해방 애니메이션 시작부터 즉사 연출(가시 VFX / 붉은 화면 / 흔들림)까지 (초, 실제 시간). 부풀었던 몸이 터지며 줄어드는 순간에 맞추기. " +
             "해방 상태 Speed가 1이면 약 1.5초, 2.5면 약 0.6초. " +
             "플레이어가 죽고 Death Delay(StageManager, 기본 1초) 뒤 화면이 멈추므로, 너무 이르면 해방 애니메이션이 끝까지 안 보임")]
    [SerializeField, Min(0f)]
    private float _deathVfxDelay = 0.6f;

    [Tooltip("즉사 연출 프리팹 (PF_BossDeathSpike). Attach To Camera면 카메라 자식, 아니면 플레이어 위치에 생성")]
    [SerializeField]
    private GameObject _deathVfxPrefab;

    [Tooltip("켜면 즉사 연출을 메인 카메라 자식으로 생성 (화면을 덮는 연출이 플레이어 / 카메라 이동에 밀리지 않고 화면에 고정)")]
    [SerializeField]
    private bool _attachDeathVfxToCamera = true;

    [Tooltip("즉사 연출 위치 보정. 카메라 자식이면 화면 중앙 기준, 아니면 플레이어 발밑 기준")]
    [SerializeField]
    private Vector2 _deathVfxOffset = Vector2.zero;

    [Tooltip("(카메라 자식일 때) 카메라 앞쪽 거리. 카메라 z가 -10이면 10 = 월드 z 0. 카메라 Near Clip보다 커야 보임")]
    [SerializeField, Min(0.01f)]
    private float _deathVfxCameraDepth = 10f;

    [Tooltip("(카메라 자식일 때) 이 카메라 크기(Orthographic Size) 기준으로 만든 연출이면 입력. " +
             "줌으로 카메라 크기가 달라져도 화면을 똑같이 덮도록 크기를 맞춤. 0이면 프리팹 크기 그대로")]
    [SerializeField, Min(0f)]
    private float _deathVfxReferenceOrthoSize = 0f;

    [Tooltip("즉사 연출이 사라지기까지 (초)")]
    [SerializeField, Min(0.1f)]
    private float _deathVfxLifetime = 3f;

    [Tooltip("즉사 연출(VFX)이 재생되는 시간 (초). VFX가 끝난 뒤 붉은 화면 / 흔들림과 함께 플레이어가 죽음. " +
             "0이면 프리팹의 파티클 / 애니메이션 길이로 자동 계산")]
    [SerializeField, Min(0f)]
    private float _deathVfxPlayDuration = 0f;

    [Tooltip("VFX가 끝난 뒤 플레이어가 죽기까지 추가 대기 (초). 0이면 VFX가 끝나는 순간 붉은 화면과 함께 바로 사망")]
    [SerializeField, Min(0f)]
    private float _killDelay = 0f;

    [Tooltip("사망 원인 표시 이름")]
    [SerializeField]
    private string _killReason = "궁극기 (가시 즉사)";

    [Tooltip("즉사 시 화면을 덮는 색. 알파 = 임팩트 직후 잠깐 남는 진하기 (여기서 Fade Out 동안 빠짐). 알파 0이면 붉은 화면 사용 안 함")]
    [SerializeField]
    private Color _deathScreenColor = new Color(0.8f, 0f, 0f, 0.55f);

    [Tooltip("임팩트 순간 붉은 화면 진하기 (0 ~ 1). 이 가장 진한 순간에 가시 VFX를 지움 (화면에 가려 사라지는 게 안 보이도록)")]
    [SerializeField, Range(0f, 1f)]
    private float _deathImpactPeakAlpha = 0.95f;

    [Tooltip("임팩트 최대 진하기를 유지하는 시간 (초, 실제 시간)")]
    [SerializeField, Min(0f)]
    private float _deathImpactHold = 0.05f;

    [Tooltip("임팩트 최대 진하기에서 Death Screen Color 알파까지 빠르게 빠지는 시간 (초)")]
    [SerializeField, Min(0.01f)]
    private float _deathImpactDrop = 0.12f;

    [Tooltip("붉은 화면이 완전히 빠지는 시간 (초)")]
    [SerializeField, Min(0.01f)]
    private float _deathScreenFadeOut = 0.8f;

    [SerializeField]
    private CameraShakeSettings _deathShake = new CameraShakeSettings(0.4f, 0.4f);

    [Header("보스")]
    [Tooltip("패턴 중 보스가 플레이어의 직접 공격을 받지 않음 (올챙이 기믹 데미지만)")]
    [SerializeField]
    private bool _blockDirectDamage = true;

    private bool[] _usedPhase1;
    private bool[] _usedPhase2;
    private bool _pending;
    private bool _isRunning;
    private bool _isJumping;
    private bool _ultimateFailed;
    private GameObject _suctionVfx;

    public override bool IsForced => true;
    // 발판 전환과 소환 위치로 이동을 동시에 시작하고, 도착한 뒤 준비 완료를 확인한다.
    protected override bool ShouldWaitForPlatformsBeforePreparation => false;
    public override bool BlocksDirectDamage => _blockDirectDamage && _isRunning;
    public override bool BlocksOutgoingDamage => true;

    /// <summary>점프 중에는 접촉 데미지 끔</summary>
    public override bool DisablesContactDamage =>
        base.DisablesContactDamage || (_isJumping && _disableCollisionWhileJumping);

    private void Reset()
    {
        SetDefaults("소환", BossPhaseMask.Both, BossFloorMask.Any);
    }

    protected override void OnInitialize()
    {
        _chargeAnimator = Boss.GetComponentInChildren<Animator>();
        _hasChargeProgressParameter = false;
        _chargeProgressHash = Animator.StringToHash(_chargeProgressParameter);
        if (_chargeAnimator != null && !string.IsNullOrEmpty(_chargeProgressParameter))
        {
            foreach (AnimatorControllerParameter parameter in _chargeAnimator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Float && parameter.nameHash == _chargeProgressHash)
                {
                    _hasChargeProgressParameter = true;
                    break;
                }
            }
            if (!_hasChargeProgressParameter)
                Debug.LogWarning($"{name}: 차지 진행도 Float 파라미터({_chargeProgressParameter})가 없습니다.", this);
        }
        _spawnExclusionCollider = Boss.GetComponent<Collider2D>();
        ResolveSuctionPoint();
        if (_bubblePrefab == null)
        {
            Debug.LogWarning($"{name}: {PatternName}에 독 방울 프리팹이 없어 사용하지 않습니다.", this);
            enabled = false;
            return;
        }

        ResetThresholds();
        Boss.Health.OnHpChanged += HandleHpChanged;
        Boss.OnPhaseChanged += HandlePhaseChanged;
    }

    private void OnDestroy()
    {
        if (Boss != null)
        {
            Boss.Health.OnHpChanged -= HandleHpChanged;
            Boss.OnPhaseChanged -= HandlePhaseChanged;
        }
    }

    private void ResetThresholds()
    {
        _usedPhase1 = new bool[_phase1HpRatios != null ? _phase1HpRatios.Length : 0];
        _usedPhase2 = new bool[_phase2HpRatios != null ? _phase2HpRatios.Length : 0];
        _pending = false;
    }

    private void OnDisable() => Stop();

    private float _lastHp = float.PositiveInfinity;

    private void HandlePhaseChanged(int phase)
    {
        // 새 페이즈는 체력이 가득 찬 상태로 시작하므로 비율 체크를 처음부터
        _usedPhase2 = new bool[_phase2HpRatios != null ? _phase2HpRatios.Length : 0];
        _pending = false;
    }

    private void HandleHpChanged(float current, float max)
    {
        // 체력이 차오를 때(2페이즈 전환 시 체력바 다시 채우기 등)는 비율 발동을 검사하지 않음
        bool increased = current > _lastHp;
        _lastHp = current;
        if (increased)
        {
            return;
        }

        if (max <= 0f || current <= 0f)
        {
            return;
        }

        float[] ratios = Boss.Phase >= 2 ? _phase2HpRatios : _phase1HpRatios;
        bool[] used = Boss.Phase >= 2 ? _usedPhase2 : _usedPhase1;
        if (ratios == null || used == null)
        {
            return;
        }

        float ratio = current / max;
        for (int i = 0; i < ratios.Length && i < used.Length; i++)
        {
            if (used[i] || ratio > ratios[i])
            {
                continue;
            }

            used[i] = true;

            // 패턴 진행 중에 넘긴 비율은 다시 발동하지 않음 (연속 발동 방지)
            if (!_isRunning)
            {
                _pending = true;
                Boss.RequestGimmick(this);
            }
        }
    }

    /// <summary>체력 비율로만 발동 (층 / 페이즈 / 쿨타임 / 가중치 무시)</summary>
    public override bool CanUse()
    {
        return isActiveAndEnabled && Boss != null && _pending && !_isRunning;
    }

    protected override void OnPatternStart()
    {
        base.OnPatternStart();
        _pending = false;
        _isRunning = true;
        _ultimateFailed = false;
    }

    /// <summary>궁극기 전에 소환 위치로 점프. 끝나면 대기 상태로 돌려 ultimate 트리거가 바로 전이되게 함</summary>
    protected override IEnumerator BeforeAnimation()
    {
        if (_summonPoint == null)
        {
            // ultimate 전이는 대기(Boss_Idle)에서만 있으므로 진행 중이던 애니메이션을 끊고 대기로
            Boss.ForceIdleAnimation();
            yield break;
        }

        Vector2 start = Boss.Position;
        Vector2 target = _summonPoint.position;

        // 점프 시작 애니메이션 (Boss의 Jump Takeoff Ratio 지점에서 이륙)
        Boss.FaceDirection(target.x - start.x);
        Boss.PlayJumpStart();
        yield return null;

        float startTimeout = Time.time + Boss.JumpStateTimeout;
        while (Boss.IsBeforeJumpTakeoff && Time.time < startTimeout)
        {
            yield return null;
        }

        // 이륙
        start = Boss.Position;
        Boss.PlayJumpAir();
        SetJumping(true);

        // 이륙 먼지 (Boss의 Takeoff Dust Timing만큼 늦게. 음수는 이륙 순간)
        if (_takeoffDust)
        {
            Boss.PlayJumpDust(start.x, start.y, Mathf.Max(0f, Boss.TakeoffDustTiming));
        }

        float landingTiming = Boss.LandingDustTiming;
        bool landingDustPlayed = !_landingDust;

        float apexY = Mathf.Max(start.y, target.y) + _jumpHeight;

        // 거리에 따라 체공 시간이 정해짐 (같은 속도로 이동)
        float minDuration = Mathf.Min(_minJumpDuration, _maxJumpDuration);
        float maxDuration = Mathf.Max(_minJumpDuration, _maxJumpDuration);
        float jumpDuration = Mathf.Clamp(Vector2.Distance(start, target) / Mathf.Max(0.1f, _jumpSpeed), minDuration, maxDuration);

        bool endAnimationPlayed = false;
        float elapsed = 0f;
        while (elapsed < jumpDuration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / jumpDuration);

            // 가로는 일정한 속도로
            float x = Mathf.Lerp(start.x, target.x, t);
            Boss.MoveTo(new Vector2(x, GetJumpY(t, start.y, apexY, target.y)));

            // 공중 애니메이션 유지, 착지 직전(Boss의 Jump End Lead Time)부터 착지 애니메이션
            endAnimationPlayed = Boss.UpdateJumpAirAnimation(jumpDuration - elapsed, endAnimationPlayed);

            // 착지 직전에 미리 (Landing Dust Timing이 음수)
            if (!landingDustPlayed && landingTiming < 0f && jumpDuration - elapsed <= -landingTiming)
            {
                Boss.PlayJumpDust(target.x, target.y);
                landingDustPlayed = true;
            }

            yield return new WaitForFixedUpdate();
        }

        // 착지
        Boss.MoveTo(target);
        if (!endAnimationPlayed)
        {
            Boss.PlayJumpEnd();
        }

        yield return new WaitForFixedUpdate();

        if (!landingDustPlayed)
        {
            Boss.PlayJumpDust(target.x, target.y, landingTiming);
        }

        SetJumping(false);
        CameraShake.Shake(_landingShake);
        Boss.FacePlayer();

        // 착지 애니메이션이 끝날 때까지
        float endTimeout = Time.time + Boss.JumpStateTimeout;
        while (Boss.IsInJumpEnd && Time.time < endTimeout)
        {
            yield return null;
        }

        // ultimate 전이는 대기(Boss_Idle)에서 확실히 시작되도록 대기 상태로
        Boss.ForceIdleAnimation();

        if (_delayAfterLanding > 0f)
        {
            yield return new WaitForSeconds(_delayAfterLanding);
        }
    }

    private void SetJumping(bool jumping)
    {
        if (jumping) AudioAction("Boss.Jump");
        _isJumping = jumping;

        if (_disableCollisionWhileJumping && Boss != null)
        {
            Boss.SetBodyCollision(!jumping);
        }
    }

    /// <summary>출발 → 최고점(t = 0.5) → 도착 높이</summary>
    private static float GetJumpY(float t, float startY, float apexY, float endY)
    {
        if (t < 0.5f)
        {
            float up = t / 0.5f;
            return Mathf.Lerp(startY, apexY, 1f - ((1f - up) * (1f - up)));
        }

        float down = (t - 0.5f) / 0.5f;
        return Mathf.Lerp(apexY, endY, down * down);
    }

    /// <summary>제한 시간 / 파훼로 끝을 직접 판단하므로 패턴 길이를 기다리지 않음</summary>
    protected override bool WaitForDurationAfterExecute => false;

    /// <summary>항상 ultimate 트리거로 시작 (인스펙터의 Animation Trigger는 사용 안 함)</summary>
    protected override string AnimationTrigger => _ultimateTrigger;

    private int _gimmickHits;
    private Image _deathScreen;
    private GameObject _deathVfxInstance;

    protected override IEnumerator Execute()
    {
        yield return ExecuteUltimate();
    }

    #region 궁극기 기믹

    /// <summary>
    /// Angry → Charge(제한 시간 동안 진행도 0 → 1) 동안 방울 생성, 올챙이 명중 횟수 집계.
    /// 횟수 달성 → 피격 애니메이션 후 종료(복귀 패턴) / 시간 초과 → 해방 + 즉사
    /// </summary>
    private IEnumerator ExecuteUltimate()
    {
        float startTime = Time.time;
        _gimmickDeadline = startTime + _maxDuration;
        if (_spawnExclusionCollider == null)
        {
            Debug.LogWarning("보스 알 생성: 몸체 Collider2D 참조가 없습니다.", this);
        }

        _gimmickHits = 0;
        BossBaby.CountHitsOnly = true;
        BossBaby.OnHitBoss -= HandleGimmickHit;
        BossBaby.OnHitBoss += HandleGimmickHit;
        SetChargeProgress(0f);

        float nextWave = Time.time + _spawnDelayAfterLoop;
        float chargeStart = -1f;
        float nextSuction = float.PositiveInfinity;
        float suctionVfxStopTime = float.PositiveInfinity;
        float chargeProgress = 0f;
        float previousChargeTime = startTime;
        bool success = false;

        while (Time.time < _gimmickDeadline)
        {
            if (_gimmickHits >= _requiredHits)
            {
                success = true;
                break;
            }

            if (Time.time >= nextWave)
            {
                SpawnWave();
                nextWave = Time.time + _waveInterval;
            }

            // 차지 상태에 들어간 순간부터 제한 시간 끝까지 진행도 0 → 1 (차지 애니메이션이 제한 시간에 맞춰 끝남)
            if (chargeStart < 0f && IsInChargeState())
            {
                chargeStart = Time.time;
                previousChargeTime = chargeStart;
                nextSuction = chargeStart + _suctionInterval;
                Boss.Log($"{PatternName}: 차지 시작 (남은 시간 {_gimmickDeadline - chargeStart:0.0}초)");
            }

            if (chargeStart >= 0f)
            {
                float delta = Time.time - previousChargeTime;
                float remaining = Mathf.Max(delta, _gimmickDeadline - previousChargeTime);
                chargeProgress = Mathf.Clamp01(chargeProgress + (1f - chargeProgress) * delta / Mathf.Max(0.01f, remaining));
                previousChargeTime = Time.time;
                SetChargeProgress(chargeProgress);
                if (Time.time >= nextSuction)
                {
                    StartBubbleSuction();
                    StartSuctionVfx();
                    suctionVfxStopTime = Time.time + _suctionDuration + _absorbDuration;
                    nextSuction = Time.time + _suctionInterval;
                }
                if (Time.time >= suctionVfxStopTime)
                {
                    StopSuctionVfx();
                    suctionVfxStopTime = float.PositiveInfinity;
                }
            }

            yield return null;
        }

        BossBaby.OnHitBoss -= HandleGimmickHit;
        BossBaby.CountHitsOnly = false;
        ClearBubbleSubscriptions();
        Boss.ClearMinions();

        // 차지가 끝났으므로(파훼 / 해방) 빨아들이기 중지
        StopSuctionVfx();

        if (success)
        {
            yield return UltimateSuccess();
        }
        else
        {
            yield return UltimateFail();
        }
    }

    private void HandleGimmickHit(Boss boss)
    {
        if (boss != Boss)
        {
            return;
        }

        _gimmickHits++;
        Boss.Log($"{PatternName}: 올챙이 명중 {_gimmickHits} / {_requiredHits}");
    }

    /// <summary>파훼: 차지를 끊고 피격 애니메이션 → 대기로 돌린 뒤 종료 (Cleanup에서 복귀 패턴 예약)</summary>
    private IEnumerator UltimateSuccess()
    {
        Boss.Log($"{PatternName}: 파훼 성공");
        Boss.ResetTrigger(_releaseTrigger);
        SetChargeProgress(0f);

        if (!Boss.PlayState(_hitStateName))
        {
            Debug.LogWarning($"{name}: {PatternName} 피격 상태({_hitStateName})가 애니메이터에 없습니다.", this);
        }

        CameraShake.Shake(_landingShake);

        float hitDuration = _hitClip != null ? _hitClip.length : _hitFallbackDuration;
        yield return new WaitForSeconds(hitDuration);

        // 찍기 등 복귀 패턴 트리거는 대기 상태에서만 전이되므로 대기로 돌림
        Boss.ForceIdleAnimation();
    }

    /// <summary>실패: 해방 애니메이션 + 즉사 연출 + 붉은 화면 → 플레이어 즉사</summary>
    private IEnumerator UltimateFail()
    {
        Boss.Log($"{PatternName}: 파훼 실패 → 즉사");
        _ultimateFailed = true;
        SetChargeProgress(1f);

        // 해방 애니메이션은 트리거(전이)에 맡기지 않고 직접 재생.
        // 차지 상태는 Motion Time(ultimateProgress)으로 시간이 고정돼 있어, 전이 설정(Exit Time 등)에 따라 트리거로 넘어가지 않는 경우가 있음
        PlayReleaseState();
        yield return null;

        if (!IsInState(_releaseStateName))
        {
            // 직접 재생이 안 되면(상태 이름이 다른 경우 등) 트리거로 한 번 더 시도
            Debug.LogWarning($"{name}: {PatternName} 해방 상태({_releaseStateName})를 직접 재생하지 못해 트리거({_releaseTrigger})로 시도합니다. " +
                             $"지금 클립: {CurrentStateDebugName()}. Release State Name을 애니메이터 상태 이름과 맞춰 주세요.", this);
            Boss.SetTrigger(_releaseTrigger);
        }
        else
        {
            Boss.Log($"{PatternName}: 해방 애니메이션 시작");
        }

        // 해방 애니메이션이 터지는 순간까지 기다렸다가 즉사 연출
        // (바로 죽이면 사망 화면이 해방 애니메이션 초반에 화면을 멈춰서 해방이 안 보임)
        if (_deathVfxDelay > 0f)
        {
            yield return new WaitForSeconds(_deathVfxDelay);
        }

        // 즉사 VFX를 먼저 끝까지 보여 준 뒤 → 붉은 화면 / 흔들림과 함께 체력이 닳아 사망
        Transform player = Boss.Player;
        float vfxDuration = SpawnDeathVfx(player);
        if (vfxDuration > 0f)
        {
            yield return new WaitForSeconds(vfxDuration);
        }

        if (_killDelay > 0f)
        {
            yield return new WaitForSeconds(_killDelay);
        }

        // 임팩트: 붉은 화면이 한 번에 가장 진하게 덮이고, 그 순간 가시 VFX를 지운 뒤 빠짐
        CameraShake.Shake(_deathShake);
        if (_deathScreenColor.a > 0f || _deathImpactPeakAlpha > 0f)
        {
            Boss.StartCoroutine(DeathScreenRoutine());
        }
        else
        {
            DestroyDeathVfx();
        }

        PlayerHealth playerHealth = player != null ? player.GetComponentInParent<PlayerHealth>() : null;
        if (playerHealth != null)
        {
            playerHealth.Kill(_killReason);
        }

        // 해방 애니메이션이 끝날 때까지 패턴 유지 (중간에 다음 패턴이 시작되지 않도록. 이후 애니메이터 전이로 대기)
        // 상태 속도(Speed)가 바뀌어도 맞도록 시간 대신 애니메이터가 해방 상태를 벗어날 때까지 기다림 (최대 Release Fallback Duration)
        float waitUntil = Time.time + _releaseFallbackDuration;
        while (IsInState(_releaseStateName) && Time.time < waitUntil)
        {
            yield return null;
        }

        Boss.ResetTrigger(_releaseTrigger);
    }

    /// <summary>차지 애니메이션이 시작되면 보스에 빨아들이기 VFX를 붙여 반복 재생</summary>
    private void StartSuctionVfx()
    {
        if (_suctionVfxPrefab == null || Boss == null || _suctionVfx != null)
        {
            return;
        }

        Transform parent = ResolveSuctionPoint();
        _suctionVfx = Instantiate(_suctionVfxPrefab, parent);
        _suctionVfx.transform.localPosition = new Vector3(_suctionVfxOffset.x, _suctionVfxOffset.y, 0f);
        _suctionVfx.transform.localRotation = Quaternion.identity;

        Vector3 scale = _suctionVfx.transform.localScale * _suctionVfxScale;
        if (_flipSuctionVfx)
        {
            scale.x = -scale.x;
        }

        _suctionVfx.transform.localScale = scale;
    }

    /// <summary>VFX를 붙일 곳: 지정한 입 위치 → 보스 아래 SuctionPoint → 보스</summary>
    private Transform ResolveSuctionPoint()
    {
        if (_suctionVfxPoint != null)
        {
            return _suctionVfxPoint;
        }

        foreach (Transform child in Boss.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "SuctionPoint")
            {
                _suctionVfxPoint = child;
                return child;
            }
        }

        return Boss.transform;
    }

    private void StopSuctionVfx()
    {
        if (_suctionVfx != null)
        {
            Destroy(_suctionVfx);
        }

        _suctionVfx = null;
    }

    /// <summary>해방 상태를 처음부터 바로 재생 (하위 상태 머신 안에 있어도 이름으로 재생)</summary>
    private void PlayReleaseState()
    {
        Animator animator = _chargeAnimator;
        if (animator == null || string.IsNullOrEmpty(_releaseStateName))
        {
            return;
        }

        Boss.ResumeAnimator();
        Boss.ResetTrigger(_releaseTrigger);
        animator.Play(Animator.StringToHash(_releaseStateName), 0, 0f);
    }

    /// <summary>애니메이터가 지금 해당 이름의 상태인지 (전이 중이면 다음 상태도 확인)</summary>
    private bool IsInState(string stateName)
    {
        if (string.IsNullOrEmpty(stateName) || Boss == null)
        {
            return false;
        }

        Animator animator = _chargeAnimator;
        if (animator == null)
        {
            return false;
        }

        if (animator.GetCurrentAnimatorStateInfo(0).IsName(stateName))
        {
            return true;
        }

        return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(stateName);
    }

    /// <summary>경고 로그용: 지금 재생 중인 클립 이름</summary>
    private string CurrentStateDebugName()
    {
        Animator animator = _chargeAnimator;
        if (animator == null)
        {
            return "(애니메이터 없음)";
        }

        AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);
        return clips.Length > 0 && clips[0].clip != null ? clips[0].clip.name : "(클립 없음)";
    }

    private bool IsInChargeState()
    {
        Animator animator = _chargeAnimator;
        if (animator != null && !string.IsNullOrEmpty(_chargeStateName))
        {
            if (animator.GetCurrentAnimatorStateInfo(0).IsName(_chargeStateName))
            {
                return true;
            }

            return animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(_chargeStateName);
        }

        return _chargeClip == null || Boss.IsPlayingClip(_chargeClip);
    }

    /// <summary>즉사 연출 생성. 카메라 자식이면 화면에 고정, 아니면 플레이어 위치. 반환값 = VFX 재생 시간 (초, 없으면 0)</summary>
    private float SpawnDeathVfx(Transform player)
    {
        if (_deathVfxPrefab == null)
        {
            return 0f;
        }

        float playDuration = _deathVfxPlayDuration > 0f ? _deathVfxPlayDuration : EstimateVfxDuration(_deathVfxPrefab);
        // 재생이 끝나기 전에 지워지지 않도록 (사망 후 화면이 멈춰도 남아 있게 약간 여유)
        float lifetime = Mathf.Max(_deathVfxLifetime, playDuration + 0.5f);

        Camera camera = _attachDeathVfxToCamera ? Camera.main : null;
        if (camera != null)
        {
            GameObject vfx = Instantiate(_deathVfxPrefab, camera.transform);
            vfx.transform.localPosition = new Vector3(_deathVfxOffset.x, _deathVfxOffset.y, _deathVfxCameraDepth);
            vfx.transform.localRotation = Quaternion.identity;

            if (_deathVfxReferenceOrthoSize > 0f && camera.orthographic)
            {
                vfx.transform.localScale *= camera.orthographicSize / _deathVfxReferenceOrthoSize;
            }

            Destroy(vfx, lifetime);
            _deathVfxInstance = vfx;
            return playDuration;
        }

        if (_attachDeathVfxToCamera)
        {
            Debug.LogWarning($"{name}: {PatternName} 메인 카메라(MainCamera 태그)를 찾지 못해 즉사 연출을 플레이어 위치에 생성합니다.", this);
        }

        if (player != null)
        {
            Vector3 position = player.position + (Vector3)_deathVfxOffset;
            GameObject vfx = Instantiate(_deathVfxPrefab, position, Quaternion.identity);
            Destroy(vfx, lifetime);
            _deathVfxInstance = vfx;
            return playDuration;
        }

        return 0f;
    }

    /// <summary>
    /// VFX 프리팹의 재생 길이 추정: 반복이 아닌 파티클(시작 지연 + 지속 + 최대 수명)과
    /// 애니메이터 / 애니메이션 클립 중 가장 긴 것. 찾지 못하면 Death Vfx Lifetime
    /// </summary>
    private float EstimateVfxDuration(GameObject prefab)
    {
        float longest = 0f;

        foreach (ParticleSystem particles in prefab.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particles.main;
            if (main.loop)
            {
                continue;
            }

            float end = main.startDelay.constantMax + main.duration + main.startLifetime.constantMax;
            longest = Mathf.Max(longest, end);
        }

        foreach (Animator animator in prefab.GetComponentsInChildren<Animator>(true))
        {
            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            if (controller == null)
            {
                continue;
            }

            float speed = animator.speed > 0f ? animator.speed : 1f;
            foreach (AnimationClip clip in controller.animationClips)
            {
                if (clip != null && !clip.isLooping)
                {
                    longest = Mathf.Max(longest, clip.length / speed);
                }
            }
        }

        return longest > 0f ? longest : _deathVfxLifetime;
    }

    private void SetChargeProgress(float value)
    {
        if (!_hasChargeProgressParameter || _chargeAnimator == null)
        {
            return;
        }

        _chargeAnimator.SetFloat(_chargeProgressHash, Mathf.Clamp01(value));
    }

    private void DestroyDeathVfx()
    {
        if (_deathVfxInstance != null)
        {
            Destroy(_deathVfxInstance);
        }

        _deathVfxInstance = null;
    }

    /// <summary>화면 전체를 붉게 확 덮었다가(임팩트) 빠짐 (런타임에 오버레이 생성)</summary>
    private IEnumerator DeathScreenRoutine()
    {
        if (_deathScreen == null)
        {
            var canvasObject = new GameObject("UltimateDeathScreen", typeof(RectTransform), typeof(Canvas));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;

            var imageObject = new GameObject("Red", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(canvasObject.transform, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _deathScreen = imageObject.GetComponent<Image>();
            _deathScreen.raycastTarget = false;
        }

        Image screen = _deathScreen;
        Color color = _deathScreenColor;
        float settleAlpha = color.a;
        float peakAlpha = Mathf.Max(_deathImpactPeakAlpha, settleAlpha);

        // 임팩트: 페이드 없이 바로 가장 진하게. 같은 프레임에 가시 VFX를 지워 붉은 화면에 가려지게
        color.a = peakAlpha;
        screen.color = color;
        DestroyDeathVfx();

        for (float t = 0f; t < _deathImpactHold && screen != null; t += Time.unscaledDeltaTime)
        {
            yield return null;
        }

        // 최대 진하기 → 남는 진하기로 빠르게 (처음이 가장 빠르게 빠지는 곡선)
        for (float t = 0f; t < _deathImpactDrop && screen != null; t += Time.unscaledDeltaTime)
        {
            float x = 1f - (t / _deathImpactDrop);
            color.a = Mathf.Lerp(settleAlpha, peakAlpha, x * x);
            screen.color = color;
            yield return null;
        }

        // 남는 진하기 → 0
        for (float t = 0f; t < _deathScreenFadeOut && screen != null; t += Time.unscaledDeltaTime)
        {
            float x = 1f - (t / _deathScreenFadeOut);
            color.a = settleAlpha * x * x;
            screen.color = color;
            yield return null;
        }

        if (screen != null)
        {
            Destroy(screen.canvas.gameObject);
        }

        if (_deathScreen == screen)
        {
            _deathScreen = null;
        }
    }

    #endregion

    private void SpawnWave()
    {
        BossArena arena = Boss.Arena;
        float left = arena != null ? arena.LeftX + _spawnXMargin : Boss.Position.x - 8f;
        float right = arena != null ? arena.RightX - _spawnXMargin : Boss.Position.x + 8f;
        if (float.IsInfinity(left) || float.IsInfinity(right) || left > right)
        {
            left = Boss.Position.x - 8f;
            right = Boss.Position.x + 8f;
        }

        float spawnY = _spawnLine != null
            ? _spawnLine.position.y
            : (arena != null ? arena.GetFloorY(1) : Boss.GroundY) - _spawnBelowFloor;
        float topY = _despawnLine != null ? _despawnLine.position.y : float.PositiveInfinity;

        if (_placedBubbleXs == null || _placedBubbleXs.Length < _bubblesPerWave)
            _placedBubbleXs = new float[_bubblesPerWave];
        float[] placed = _placedBubbleXs;
        int placedCount = 0;

        for (int i = 0; i < _bubblesPerWave; i++)
        {
            // 같은 웨이브끼리 너무 붙지 않게 몇 번 다시 뽑아 봄
            float x = Random.Range(left, right);
            for (int attempt = 0; attempt < MaxSpawnAttempts && (IsTooClose(x, placed, placedCount) || IsBelowBoss(x)); attempt++)
            {
                x = Random.Range(left, right);
            }
            if (IsTooClose(x, placed, placedCount) || IsBelowBoss(x)) continue;

            placed[placedCount++] = x;

            BossPoisonBubble bubble = Instantiate(_bubblePrefab, new Vector3(x, spawnY, 0f), Quaternion.identity);
            bubble.Initialize(Boss, topY);
            bubble.OnAbsorbed += HandleBubbleAbsorbed;
            _activeBubbles.Add(bubble);
        }
        if (placedCount > 0) AudioResult("Boss.BubbleSpawn", Boss.transform.position);
    }

    private bool IsTooClose(float x, float[] placed, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (Mathf.Abs(placed[i] - x) < _minSpacing)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsBelowBoss(float x)
    {
        if (_spawnExclusionCollider == null) return true;
        Bounds bounds = _spawnExclusionCollider.bounds;
        return x >= bounds.min.x - _bossSpawnExclusionMargin && x <= bounds.max.x + _bossSpawnExclusionMargin;
    }

    private void StartBubbleSuction()
    {
        ResolveSuctionPoint();
        if (_suctionVfxPoint == null)
        {
            Debug.LogWarning("보스 알 흡입: SuctionPoint 참조가 없습니다.", this);
            return;
        }
        for (int i = _activeBubbles.Count - 1; i >= 0; i--)
        {
            BossPoisonBubble bubble = _activeBubbles[i];
            if (bubble == null)
            {
                _activeBubbles.RemoveAt(i);
                continue;
            }
            bubble.StartSuctionPulse(_suctionVfxPoint, _suctionDistance, _suctionDuration, _absorbRadius, _absorbDuration);
        }
    }

    private void HandleBubbleAbsorbed()
    {
        _gimmickDeadline -= _absorbedTimeReduction;
    }

    private void ClearBubbleSubscriptions()
    {
        foreach (BossPoisonBubble bubble in _activeBubbles)
        {
            if (bubble != null) bubble.OnAbsorbed -= HandleBubbleAbsorbed;
        }
        _activeBubbles.Clear();
    }

    protected override void Cleanup()
    {
        ClearBubbleSubscriptions();
        // 차지 중에 끊겼으면 빨아들이기 VFX 제거
        StopSuctionVfx();

        // 궁극기 기믹 중에 끊겼으면 집계 해제
        BossBaby.OnHitBoss -= HandleGimmickHit;
        BossBaby.CountHitsOnly = false;

        // 점프 중에 끊겼으면 충돌 원래대로
        if (_isJumping)
        {
            SetJumping(false);
        }

        // 끝났거나 끊겼으면 남은 방울 / 올챙이 모두 제거하고, 복귀 패턴(찍기)을 바로 이어서 사용
        if (_isRunning && Boss != null)
        {
            Boss.ClearMinions();

            // 즉사(실패)했으면 복귀 패턴 없음 (해방 애니메이션 도중 찍기가 시작되지 않도록)
            if (_returnPattern != null && !_ultimateFailed)
            {
                Boss.QueuePattern(_returnPattern);
            }
        }

        _isRunning = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (_summonPoint != null)
        {
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.9f);
            Gizmos.DrawWireSphere(_summonPoint.position, 0.5f);
        }

        Gizmos.color = new Color(0.5f, 1f, 0.3f, 0.8f);
        if (_spawnLine != null)
        {
            Vector3 p = _spawnLine.position;
            Gizmos.DrawLine(p + (Vector3.left * 15f), p + (Vector3.right * 15f));
        }

        Gizmos.color = new Color(0.5f, 1f, 0.3f, 0.4f);
        if (_despawnLine != null)
        {
            Vector3 p = _despawnLine.position;
            Gizmos.DrawLine(p + (Vector3.left * 15f), p + (Vector3.right * 15f));
        }
    }
}
