using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 허브. 상태 머신, 패턴 선택, 페이즈, 애니메이션 / 방향 / 이동 도우미.
/// - 패턴은 BossPattern을 상속한 컴포넌트로 보스(또는 자식)에 붙이면 자동 등록
/// - 상태: 대기 → 등장 → [생각 ↔ 패턴] → (패링) 그로기 / (1페 체력 0) 하울링 → 2페 → (2페 체력 0) 완전 그로기 → 마무리 → 처치
/// - 완전 그로기 중 플레이어 마무리 공격의 대상 (IFinishable)
/// </summary>
[RequireComponent(typeof(BossHealth))]
public class Boss : MonoBehaviour, IFinishable
{
    [Header("기본")]
    [SerializeField]
    private string _bossName = "보스";

    [Tooltip("비어 있으면 씬에서 자동으로 찾음")]
    [SerializeField]
    private BossArena _arena;

    [Tooltip("스프라이트 원본이 오른쪽을 보고 있으면 체크")]
    [SerializeField]
    private bool _spriteFacesRight = false;

    [Tooltip("체크하면 씬 시작 시 바로 전투 시작. 끄면 StartFight()를 호출해야 시작 (스테이지 플로우 연결용)")]
    [SerializeField]
    private bool _startOnAwake = true;

    [Tooltip("등장 연출 시간 (초)")]
    [SerializeField, Min(0f)]
    private float _introDuration = 1.5f;

    [Tooltip("등장 연출(하울링)에서도 입 이펙트(BossHowl) 재생. 타이밍은 Howl Vfx Delay를 같이 사용")]
    [SerializeField]
    private bool _playIntroHowlVfx = true;

    [Tooltip("전투 시작 위치 (선택, 보스 발밑 기준). 지정하면 씬 시작 시 이 위치에서 대기하고 이 자리에서 등장 하울링")]
    [SerializeField]
    private Transform _introPoint;

    [Tooltip("등장 하울링이 끝나면 바로 쓸 첫 패턴 (예: 찍기). 소환 후 복귀처럼 조건 / 대기 시간 무시. 비우면 평소처럼 대기 후 랜덤 패턴")]
    [SerializeField]
    private BossPattern _introNextPattern;

    [Tooltip("보스가 착지하는 바닥 높이 기준점 (선택, 보스 발밑 기준). 비우면: Intro Point를 쓸 때는 BossArena의 1층 바닥, 아니면 씬에 배치한 보스 위치")]
    [SerializeField]
    private Transform _groundPoint;

    [Header("패턴 사이 대기 (초, 최소 ~ 최대)")]
    [SerializeField]
    private Vector2 _phase1IdleRange = new Vector2(0.8f, 1.4f);

    [SerializeField]
    private Vector2 _phase2IdleRange = new Vector2(0.5f, 1.0f);

    [Header("페이즈 전환 (하울링)")]
    [Tooltip("하울링 연출 시간 (초). 이 시간 동안 체력바가 다시 차오름")]
    [SerializeField, Min(0f)]
    private float _howlDuration = 2.5f;

    [Tooltip("하울링 시작 후 입 이펙트(BossHowl)가 터지기까지 (초). 등장 / 페이즈 전환 하울링 공용. 애니메이션에서 입을 벌리는 타이밍에 맞춤")]
    [SerializeField, Min(0f)]
    private float _howlVfxDelay = 0f;

    [SerializeField]
    private CameraShakeSettings _howlShake = new CameraShakeSettings(1.2f, 0.25f);

    [SerializeField]
    private CameraZoomSettings _howlZoom = new CameraZoomSettings(0.08f, 0.3f, 1.4f, 0.6f, 0.2f, 2f);

    [Header("입")]
    [Tooltip("입 위치 (하울링 이펙트 등). 비어 있으면 자식 중 이름이 \"Mouth\"인 오브젝트를 찾고, 없으면 몸 중심 사용")]
    [SerializeField]
    private Transform _mouth;

    [Header("처치")]
    [Tooltip("마무리 공격을 맞은 뒤 사라질 때까지 (초)")]
    [SerializeField, Min(0f)]
    private float _defeatDuration = 2f;

    [Tooltip("켜면 마무리 공격에 맞았을 때 지상 적처럼 회전하며 작아지고 흐려지면서 날아감")]
    [SerializeField]
    private bool _launchOnFinisher = true;

    [Tooltip("켜면 아래 날려 보내기 설정을 사용. 끄면 플레이어 마무리 공격(PlayerFinisher)의 설정을 그대로 사용 (지상 적과 동일)")]
    [SerializeField]
    private bool _overrideLaunchSettings = false;

    [SerializeField]
    private LaunchSettings _launchSettings = LaunchSettings.Default;

    [Tooltip("처치 연출 뒤 보스 오브젝트를 끔")]
    [SerializeField]
    private bool _disableOnDefeat = true;

    [Tooltip("처치 소울을 한 번에 뿌리는 개수 (일반 적은 5개). 0이면 뿌리지 않음")]
    [SerializeField, Min(0)]
    private int _deathSoulsPerBurst = 8;

    [Tooltip("처치 소울을 뿌리는 횟수. 전체 개수 = 한 번에 뿌리는 개수 x 횟수")]
    [SerializeField, Min(1)]
    private int _deathSoulBursts = 5;

    [Tooltip("처치 소울을 뿌리는 간격 (초)")]
    [SerializeField, Min(0f)]
    private float _deathSoulBurstInterval = 0.12f;

    [Tooltip("처치 소울 생성 위치가 흩어지는 반경. 회차마다 터지는 중심이 이 안에서 무작위 (0이면 한 점에서)")]
    [SerializeField, Min(0f)]
    private float _deathSoulSpawnRadius = 1.2f;

    [Tooltip("처치 소울 전체 충전량 (1 = 소울 슬롯 1칸, 일반 적은 1)")]
    [SerializeField, Min(0f)]
    private float _deathSoulTotalAmount = 5f;

    [Tooltip("소울이 퍼지는 속도 배율 (많이 뿌릴수록 크게 하면 넓게 퍼짐)")]
    [SerializeField, Min(0.1f)]
    private float _deathSoulSpeedMultiplier = 1.6f;

    [Header("애니메이션 파라미터 (애니메이터에 없으면 무시)")]
    [SerializeField]
    private string _introTrigger = "intro";

    [Tooltip("그로기 시작 시 보낼 트리거 (비우면 사용 안 함)")]
    [SerializeField]
    private string _groggyTrigger = "";

    [Tooltip("그로기 중 켜 둘 Bool (비우면 사용 안 함)")]
    [SerializeField]
    private string _groggyBool = "isGroggy";

    [Tooltip("짧은 그로기가 끝날 때 보낼 트리거 (비우면 사용 안 함)")]
    [SerializeField]
    private string _groggyEndTrigger = "groggyEnd";

    [Tooltip("groggyEnd 이후 재생되는 일어나는 애니메이션 클립. 이 길이만큼 기다린 뒤 다음 패턴을 고름")]
    [SerializeField]
    private AnimationClip _groggyEndClip;

    [Tooltip("일어나는 클립이 없을 때 기다리는 시간 (초)")]
    [SerializeField, Min(0f)]
    private float _groggyEndFallbackDuration = 0.5f;

    [SerializeField]
    private string _howlTrigger = "howl";

    [Tooltip("대기 애니메이터 상태 이름. 하울링(페이즈 전환) 시 진행 중인 애니메이션을 끊고 이 상태로 돌린 뒤 하울링 트리거를 보냄")]
    [SerializeField]
    private string _idleStateName = "Boss_Idle";

    [SerializeField]
    private string _defeatTrigger = "defeat";

    [Tooltip("처치 직전(완전 그로기, 마무리 공격 대기) 애니메이터 트리거. 비우면 그로기 애니메이션 사용")]
    [SerializeField]
    private string _finalGroggyTrigger = "die";

    [Header("처치 직전 표시 (마무리 공격 대기 중 머리 위)")]
    [Tooltip("마무리 공격 대기(완전 그로기) 동안 머리 위에 띄울 이펙트 (PF_Boss_Here). 비우면 표시 안 함")]
    [SerializeField]
    private GameObject _finishMarkerPrefab;

    [Tooltip("표시 위치 기준점 (선택). 비우면 몸통 콜라이더 맨 위(머리)")]
    [SerializeField]
    private Transform _finishMarkerPoint;

    [Tooltip("기준점에서 추가로 옮길 위치 (y가 클수록 더 위)")]
    [SerializeField]
    private Vector2 _finishMarkerOffset = new Vector2(0f, 0.5f);

    [Header("점프 / 착지 먼지 (PF_Boss_JumpDust_VFX, 짧은 점프 / 소환 점프 / 찍기 공통)")]
    [Tooltip("먼지 크기 배율")]
    [SerializeField, Min(0f)]
    private float _jumpDustScale = 1f;

    [Tooltip("0보다 크면 발밑 중심에서 좌우로 이만큼 떨어진 곳에 하나씩 (바깥쪽을 보도록 반전). 0이면 가운데 하나")]
    [SerializeField, Min(0f)]
    private float _jumpDustSpread = 0f;

    [Tooltip("먼지 위치 높이 보정 (발밑 기준)")]
    [SerializeField]
    private float _jumpDustOffsetY = 0f;

    [Tooltip("이륙 먼지 타이밍 (초). 0 = 땅에서 떨어지는 순간, 양수 = 그만큼 늦게, 음수 = 그만큼 일찍 (짧은 점프만 일찍 가능, 나머지는 시작 순간)")]
    [SerializeField]
    private float _takeoffDustTiming = 0f;

    [Tooltip("착지 먼지 타이밍 (초). 0 = 땅에 닿는 순간, 음수 = 그만큼 일찍(착지 직전), 양수 = 그만큼 늦게")]
    [SerializeField]
    private float _landingDustTiming = 0f;

    [Header("디버그")]
    [SerializeField]
    private bool _logState = true;

    [Header("상태 (확인용)")]
    [SerializeField, ReadOnly]
    private string _currentStateName;

    [SerializeField, ReadOnly]
    private int _phase = 1;

    [SerializeField, ReadOnly]
    private int _playerFloor = 1;

    /// <summary>전투 시작 (UI, 스테이지 플로우 연결용)</summary>
    public static event Action<Boss> OnFightStarted;

    /// <summary>마무리 공격으로 처치됨 (스테이지 클리어 연결용)</summary>
    public static event Action<Boss> OnDefeated;

    /// <summary>페이즈 변경 (2 = 2페이즈 시작)</summary>
    public event Action<int> OnPhaseChanged;

    /// <summary>처치 연출(날려 보내기 / 처치 애니메이션)이 끝남. 보스가 꺼지기 직전</summary>
    public event Action OnDefeatFinished;
    public event Action<BossState, BossState> OnAudioStateChanged;

    /// <summary>true인 동안 등장 상태에서 패턴으로 넘어가지 않음 (등장 연출이 끝날 때까지 대기)</summary>
    public bool HoldIntro { get; set; }

    /// <summary>true면 전투 시작 시 등장 하울링(등장 상태)을 건너뛰고 바로 대기 → 패턴으로 (리스폰 시 연출 생략용)</summary>
    public bool SkipIntro { get; set; }

    private readonly BossStateMachine _stateMachine = new BossStateMachine();
    private readonly List<BossPattern> _patterns = new List<BossPattern>();
    private readonly List<BossPattern> _candidates = new List<BossPattern>();
    private readonly HashSet<int> _animatorParameters = new HashSet<int>();
    private readonly List<GameObject> _minions = new List<GameObject>();
    private readonly List<AnimatorClipInfo> _clipInfos = new List<AnimatorClipInfo>();

    private BossHealth _health;
    private Animator _animator;
    private Rigidbody2D _body;
    private Collider2D _bodyCollider;
    private bool _isBodyCollisionOff;
    private bool _bodyWasTrigger;
    private Transform _player;
    private BossPattern _lastPattern;
    private BossPattern _queuedPattern;
    private bool _isAnimatorPaused;
    private float _animatorSpeedBeforePause = 1f;

    #region 프로퍼티

    public string BossName => _bossName;
    public BossArena Arena => _arena;
    public BossHealth Health => _health;

    /// <summary>패링 그로기 공격 직전 반짝임 (없으면 null)</summary>
    public ParryWarningFlash ParryWarning { get; private set; }
    public Transform Player => _player;
    public int Phase => _phase;
    public BossState CurrentState => _stateMachine.CurrentState;
    public float GroundY { get; private set; }

    public float IntroDuration => _introDuration;
    public bool PlayIntroHowlVfx => _playIntroHowlVfx;
    public float HowlDuration => _howlDuration;
    public float HowlVfxDelay => _howlVfxDelay;
    public CameraShakeSettings HowlShake => _howlShake;
    public CameraZoomSettings HowlZoom => _howlZoom;
    public float DefeatDuration => _defeatDuration;
    public bool DisableOnDefeat => _disableOnDefeat;
    public int DeathSoulsPerBurst => _deathSoulsPerBurst;
    public int DeathSoulBursts => _deathSoulBursts;
    public float DeathSoulBurstInterval => _deathSoulBurstInterval;
    public float DeathSoulSpawnRadius => _deathSoulSpawnRadius;
    public float DeathSoulTotalAmount => _deathSoulTotalAmount;
    public float DeathSoulSpeedMultiplier => _deathSoulSpeedMultiplier;

    public string IntroTrigger => _introTrigger;
    public string GroggyTrigger => _groggyTrigger;
    public string GroggyBool => _groggyBool;
    public string GroggyEndTrigger => _groggyEndTrigger;

    /// <summary>그로기에서 일어나는 애니메이션 길이 (초)</summary>
    public float GroggyEndDuration => _groggyEndClip != null ? _groggyEndClip.length : _groggyEndFallbackDuration;
    public string HowlTrigger => _howlTrigger;
    public string DefeatTrigger => _defeatTrigger;
    public string FinalGroggyTrigger => _finalGroggyTrigger;

    /// <summary>몸통 접촉 데미지를 줄 수 있는지 (대기 / 패턴 중일 때만, 접촉 데미지를 끄는 패턴 중에는 제외)</summary>
    public bool CanDealContactDamage =>
        CurrentState is BossIdleState ||
        (CurrentState is BossPatternState patternState && !patternState.Pattern.DisablesContactDamage);

    /// <summary>지금 상태에서 데미지를 받는지</summary>
    public bool CanTakeDamage => CurrentState != null && CurrentState.CanTakeDamage;

    /// <summary>플레이어의 직접 공격을 받는지 (소환 패턴 중에는 막힘, 기믹 데미지는 CanTakeDamage 기준)</summary>
    public bool CanTakeDirectDamage =>
        CanTakeDamage && !(CurrentState is BossPatternState patternState && patternState.Pattern.BlocksDirectDamage);

    /// <summary>지금 바로 써야 하는 패턴(체력 비율 발동 등)이 있는지</summary>
    public bool HasForcedPattern
    {
        get
        {
            if (_queuedPattern != null)
            {
                return true;
            }

            foreach (BossPattern pattern in _patterns)
            {
                if (pattern != null && pattern.IsForced && pattern.CanUse())
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>바라보는 방향 (1 = 오른쪽, -1 = 왼쪽)</summary>
    public float FacingX => (transform.localScale.x >= 0f) == _spriteFacesRight ? 1f : -1f;

    /// <summary>몸 중심 (콜라이더 중심)</summary>
    public Vector2 CenterPosition =>
        _bodyCollider != null ? (Vector2)_bodyCollider.bounds.center : (Vector2)transform.position;

    /// <summary>입 위치 (입이 지정되지 않았으면 몸 중심)</summary>
    public Vector2 MouthPosition => _mouth != null ? (Vector2)_mouth.position : CenterPosition;

    /// <summary>오른쪽을 보고 있는지</summary>
    public bool IsFacingRight => FacingX > 0f;

    /// <summary>살아 있는 소환물 수 (독 방울 + 올챙이)</summary>
    public int MinionCount
    {
        get
        {
            _minions.RemoveAll(minion => minion == null);
            return _minions.Count;
        }
    }

    #endregion

    #region 생명 주기

    private void Awake()
    {
        _health = GetComponent<BossHealth>();
        ParryWarning = GetComponentInChildren<ParryWarningFlash>(true);
        _animator = GetComponentInChildren<Animator>();
        _body = GetComponent<Rigidbody2D>();
        _bodyCollider = GetComponent<Collider2D>();
        GroundY = transform.position.y;

        if (_mouth == null)
        {
            _mouth = FindChildRecursive(transform, "Mouth");
        }

        // 보스는 스크립트로만 움직임. Dynamic이면 중력 / 플레이어와의 충돌로 튀어 오를 수 있음
        if (_body != null && _body.bodyType != RigidbodyType2D.Kinematic)
        {
            Debug.LogWarning($"{name}: 보스 Rigidbody2D를 Kinematic으로 바꿉니다. 프리팹에서도 Kinematic으로 설정해 주세요.", this);
            _body.bodyType = RigidbodyType2D.Kinematic;
            _body.linearVelocity = Vector2.zero;
        }

        CacheAnimatorParameters();

        // 애니메이터가 루트에 있으면 클립의 위치 키가 보스 이동을 덮어씀 (점프하는 것처럼 보임)
        if (_animator != null && _animator.gameObject == gameObject)
        {
            Debug.LogWarning($"{name}: Animator가 보스 루트에 있습니다. 애니메이션에 위치(Position) 키가 있으면 패턴 이동과 충돌하니 스프라이트 자식으로 옮겨 주세요.", this);
        }

        GetComponentsInChildren(true, _patterns);
        foreach (BossPattern pattern in _patterns)
        {
            pattern.Initialize(this);
        }

        if (_patterns.Count == 0)
        {
            Debug.LogWarning($"{name}: 등록된 패턴이 없습니다. BossPattern 컴포넌트를 보스에 붙여 주세요.", this);
        }

        _stateMachine.ChangeState(new BossWaitState(this));
        if (!TryGetComponent<BossAudio>(out _)) gameObject.AddComponent<BossAudio>();
    }

    private void Start()
    {
        var player = GameObject.FindWithTag(Constants.Tags.Player);
        if (player != null)
        {
            _player = player.transform;
        }
        else
        {
            Debug.LogWarning($"{name}: Player 태그 오브젝트를 찾지 못했습니다.", this);
        }

        if (_arena == null)
        {
            _arena = FindFirstObjectByType<BossArena>();
            if (_arena == null)
            {
                Debug.LogWarning($"{name}: BossArena가 없어 플레이어 층을 항상 1층으로 판단합니다.", this);
            }
        }

        ResolveGroundY();

        // 전투 시작 위치에서 대기 (등장 연출 카메라가 보스를 비출 때 이미 그 자리에 있도록)
        MoveToIntroPoint();

        if (_startOnAwake)
        {
            StartFight();
        }
    }

    /// <summary>
    /// 착지 바닥 높이(GroundY) 결정. 시작 위치가 높은 곳이어도 점프 / 착지는 바닥 기준으로 하도록
    /// Ground Point → (Intro Point 사용 시) BossArena 1층 바닥 → 씬 배치 위치 순
    /// </summary>
    private void ResolveGroundY()
    {
        if (_groundPoint != null)
        {
            GroundY = _groundPoint.position.y;
            return;
        }

        if (_introPoint != null && _arena != null && _arena.TryGetFloorY(1, out float floorY))
        {
            GroundY = floorY;
        }
    }

    /// <summary>전투 시작 위치(Intro Point)로 즉시 이동. 지정 안 했으면 무시</summary>
    private void MoveToIntroPoint()
    {
        if (_introPoint == null)
        {
            return;
        }

        Vector2 position = _introPoint.position;
        if (_body != null)
        {
            _body.position = position;
        }

        transform.position = new Vector3(position.x, position.y, transform.position.z);
    }

    private void Update()
    {
        if (_player != null && _arena != null)
        {
            _playerFloor = _arena.GetFloor(_player.position);
        }

        _stateMachine.Update();
        _currentStateName = CurrentState != null ? CurrentState.GetType().Name : "-";
    }

    private void OnDisable()
    {
        StageBossFightGate.Cancel(this);
        ClearMinions();
        HideFinishMarker();
    }

    #endregion

    #region 흐름

    /// <summary>전투 시작. 대기 상태일 때만 동작 (스테이지 플로우에서 호출)</summary>
    public void StartFight()
    {
        if (!(CurrentState is BossWaitState))
        {
            return;
        }

        if (StageBossFightGate.TryDefer(this))
        {
            return;
        }

        _phase = 1;
        _health.BeginFight();
        MoveToIntroPoint();

        // 하울링이 끝나면 바로 이어서 쓸 첫 패턴 (예: 시작 위치에서 뛰어내리며 찍기)
        if (_introNextPattern != null)
        {
            QueuePattern(_introNextPattern);
        }

        OnFightStarted?.Invoke(this);

        // 전투 시작 이벤트에서 연출 쪽이 SkipIntro를 켰다면 하울링 없이 바로 전투
        if (SkipIntro)
        {
            Log("등장 하울링 생략");
            ForceIdleAnimation();
            GoIdle();
            return;
        }

        ChangeState(new BossIntroState(this));
    }

    public void ChangeState(BossState nextState)
    {
        BossState previous = CurrentState;
        _stateMachine.ChangeState(nextState);
        OnAudioStateChanged?.Invoke(previous, nextState);
    }

    /// <summary>패턴 사이 대기로 (페이즈별 대기 시간)</summary>
    public void GoIdle()
    {
        Vector2 range = _phase >= 2 ? _phase2IdleRange : _phase1IdleRange;
        ChangeState(new BossIdleState(this, UnityEngine.Random.Range(range.x, Mathf.Max(range.x, range.y))));
    }

    /// <summary>짧은 그로기 (찍기 패링 등). 패턴 / 대기 중일 때만</summary>
    public void EnterGroggy(float duration)
    {
        if (CurrentState == null || !CurrentState.CanBeInterrupted)
        {
            return;
        }

        ChangeState(new BossGroggyState(this, duration));
    }

    /// <summary>BossHealth가 체력 0일 때 호출</summary>
    public void HandleHealthDepleted()
    {
        if (_phase <= 1)
        {
            ChangeState(new BossPhaseTransitionState(this));
        }
        else
        {
            ChangeState(new BossFinalGroggyState(this));
        }
    }

    public void SetPhase(int phase)
    {
        _phase = phase;
        _lastPattern = null;
        _queuedPattern = null;
        OnPhaseChanged?.Invoke(phase);
    }

    public void NotifyDefeated()
    {
        OnDefeated?.Invoke(this);
    }

    public void NotifyDefeatFinished()
    {
        OnDefeatFinished?.Invoke();
    }

    #endregion

    #region 패턴 선택

    /// <summary>지금 쓸 수 있는 패턴 중 가중치로 하나 선택 (같은 패턴 연속 사용은 다른 후보가 있으면 피함)</summary>
    public bool TrySelectPattern(out BossPattern selected)
    {
        selected = null;
        _candidates.Clear();

        // 예약된 패턴(소환 후 찍기로 복귀 등)이 가장 먼저. 조건(층 / 쿨타임 등) 무시
        if (_queuedPattern != null)
        {
            BossPattern queued = _queuedPattern;
            _queuedPattern = null;

            if (queued.isActiveAndEnabled)
            {
                selected = queued;
                _lastPattern = selected;
                return true;
            }
        }

        // 체력 비율 발동 등 무조건 써야 하는 패턴이 먼저
        foreach (BossPattern pattern in _patterns)
        {
            if (pattern != null && pattern.IsForced && pattern.CanUse())
            {
                selected = pattern;
                _lastPattern = selected;
                return true;
            }
        }

        foreach (BossPattern pattern in _patterns)
        {
            if (pattern != null && !pattern.IsForced && pattern.CanUse())
            {
                _candidates.Add(pattern);
            }
        }

        if (_candidates.Count == 0)
        {
            return false;
        }

        if (_candidates.Count > 1 && _lastPattern != null)
        {
            _candidates.Remove(_lastPattern);
        }

        float totalWeight = 0f;
        foreach (BossPattern pattern in _candidates)
        {
            totalWeight += pattern.Weight;
        }

        float pick = UnityEngine.Random.value * totalWeight;
        foreach (BossPattern pattern in _candidates)
        {
            pick -= pattern.Weight;
            if (pick <= 0f)
            {
                selected = pattern;
                break;
            }
        }

        if (selected == null)
        {
            selected = _candidates[_candidates.Count - 1];
        }

        _lastPattern = selected;
        return true;
    }

    /// <summary>다음 패턴을 지정 (대기 시간 없이 바로, 사용 조건 무시). 페이즈가 바뀌면 취소</summary>
    public void QueuePattern(BossPattern pattern)
    {
        _queuedPattern = pattern;
    }

    /// <summary>플레이어가 있는 층 (1 ~ 3)</summary>
    public int GetPlayerFloor()
    {
        return _playerFloor;
    }

    #endregion

    #region 이동 / 방향

    public void FacePlayer()
    {
        if (_player != null)
        {
            FaceDirection(_player.position.x - transform.position.x);
        }
    }

    /// <summary>directionX 쪽을 바라봄 (양수 = 오른쪽)</summary>
    public void FaceDirection(float directionX)
    {
        if (Mathf.Abs(directionX) < 0.01f)
        {
            return;
        }

        float sign = directionX > 0f ? 1f : -1f;
        if (!_spriteFacesRight)
        {
            sign = -sign;
        }

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * sign;
        transform.localScale = scale;
    }

    /// <summary>물리 이동 (패턴에서 FixedUpdate 타이밍에 호출)</summary>
    public void MoveTo(Vector2 position)
    {
        if (_body != null)
        {
            _body.MovePosition(position);
        }
        else
        {
            transform.position = position;
        }
    }

    public Vector2 Position => _body != null ? _body.position : (Vector2)transform.position;

    /// <summary>공중에 떠 있으면 바로 땅으로 (패턴이 중간에 끊겼을 때)</summary>
    public float TakeoffDustTiming => _takeoffDustTiming;
    public float LandingDustTiming => _landingDustTiming;

    /// <summary>delay초 뒤에 발밑 먼지 (0 이하면 바로)</summary>
    public void PlayJumpDust(float x, float footY, float delay)
    {
        if (delay <= 0f || !isActiveAndEnabled)
        {
            PlayJumpDust(x, footY);
            return;
        }

        StartCoroutine(PlayJumpDustAfter(x, footY, delay));
    }

    private IEnumerator PlayJumpDustAfter(float x, float footY, float delay)
    {
        yield return new WaitForSeconds(delay);
        PlayJumpDust(x, footY);
    }

    /// <summary>발밑 점프 / 착지 먼지 (x, 발밑 높이)</summary>
    public void PlayJumpDust(float x, float footY)
    {
        EffectManager effects = EffectManager.Instance;
        if (effects == null || _jumpDustScale <= 0f)
        {
            return;
        }

        float y = footY + _jumpDustOffsetY;
        if (_jumpDustSpread <= 0f)
        {
            effects.PlayBossJumpDust(new Vector2(x, y), _jumpDustScale, FacingX < 0f);
            return;
        }

        effects.PlayBossJumpDust(new Vector2(x - _jumpDustSpread, y), _jumpDustScale, true);
        effects.PlayBossJumpDust(new Vector2(x + _jumpDustSpread, y), _jumpDustScale, false);
    }

    public void SnapToGround()
    {
        Vector2 position = Position;
        if (Mathf.Approximately(position.y, GroundY))
        {
            return;
        }

        position.y = GroundY;
        if (_body != null)
        {
            _body.position = position;
        }

        transform.position = new Vector3(position.x, position.y, transform.position.z);
    }

    #endregion

    #region 소환물

    public void RegisterMinion(GameObject minion)
    {
        if (minion != null && !_minions.Contains(minion))
        {
            _minions.Add(minion);
        }
    }

    public void UnregisterMinion(GameObject minion)
    {
        _minions.Remove(minion);
    }

    /// <summary>남은 독 방울 / 아기 모두 제거 (페이즈 전환, 완전 그로기 시)</summary>
    private GameObject _finishMarker;

    /// <summary>머리 위 처치 직전 표시 위치</summary>
    public Vector2 FinishMarkerPosition
    {
        get
        {
            Vector2 basePosition;
            if (_finishMarkerPoint != null)
            {
                basePosition = _finishMarkerPoint.position;
            }
            else if (_bodyCollider != null)
            {
                Bounds bounds = _bodyCollider.bounds;
                basePosition = new Vector2(bounds.center.x, bounds.max.y);
            }
            else
            {
                basePosition = transform.position;
            }

            return basePosition + _finishMarkerOffset;
        }
    }

    /// <summary>마무리 공격 대기 표시를 머리 위에 띄움 (BossFinalGroggyState에서 호출)</summary>
    public void ShowFinishMarker()
    {
        if (_finishMarkerPrefab == null)
        {
            return;
        }

        if (_finishMarker == null)
        {
            // 보스 자식으로 두면 좌우 반전(스케일)을 같이 받으므로 따로 생성
            _finishMarker = Instantiate(_finishMarkerPrefab);
        }

        _finishMarker.transform.position = FinishMarkerPosition;
        _finishMarker.SetActive(true);
    }

    public void HideFinishMarker()
    {
        if (_finishMarker != null)
        {
            Destroy(_finishMarker);
            _finishMarker = null;
        }
    }

    public void ClearMinions()
    {
        for (int i = _minions.Count - 1; i >= 0; i--)
        {
            if (_minions[i] != null)
            {
                Destroy(_minions[i]);
            }
        }

        _minions.Clear();
    }

    #endregion

    #region 애니메이션

    public void SetTrigger(string parameter)
    {
        if (TryGetParameter(parameter, out int hash))
        {
            _animator.SetTrigger(hash);
        }
    }

    /// <summary>애니메이터가 지금 이 클립을 재생 중인지 (전이 중이면 다음 상태도 확인)</summary>
    public bool IsPlayingClip(AnimationClip clip)
    {
        if (_animator == null || clip == null || !_animator.isActiveAndEnabled)
        {
            return false;
        }

        _clipInfos.Clear();
        _animator.GetCurrentAnimatorClipInfo(0, _clipInfos);
        if (ContainsClip(_clipInfos, clip))
        {
            return true;
        }

        if (_animator.IsInTransition(0))
        {
            _clipInfos.Clear();
            _animator.GetNextAnimatorClipInfo(0, _clipInfos);
            return ContainsClip(_clipInfos, clip);
        }

        return false;
    }

    public bool HasAnimator => _animator != null && _animator.runtimeAnimatorController != null;

    /// <summary>이 클립을 재생 중인 상태의 해시와 진행도(0~1)를 가져옴 (전이 중이면 다음 상태도 확인)</summary>
    public bool TryGetClipState(AnimationClip clip, out int stateHash, out float normalizedTime)
    {
        stateHash = 0;
        normalizedTime = 0f;
        if (_animator == null || clip == null || !_animator.isActiveAndEnabled)
        {
            return false;
        }

        AnimatorStateInfo info;
        _clipInfos.Clear();
        _animator.GetCurrentAnimatorClipInfo(0, _clipInfos);
        if (ContainsClip(_clipInfos, clip))
        {
            info = _animator.GetCurrentAnimatorStateInfo(0);
        }
        else if (_animator.IsInTransition(0))
        {
            _clipInfos.Clear();
            _animator.GetNextAnimatorClipInfo(0, _clipInfos);
            if (!ContainsClip(_clipInfos, clip))
            {
                return false;
            }

            info = _animator.GetNextAnimatorStateInfo(0);
        }
        else
        {
            return false;
        }

        stateHash = info.fullPathHash;
        normalizedTime = clip.isLooping ? Mathf.Repeat(info.normalizedTime, 1f) : Mathf.Clamp01(info.normalizedTime);
        return true;
    }

    /// <summary>애니메이터를 멈추고 해당 상태의 특정 지점(0~1) 포즈로 고정 (역재생 등 직접 시간을 조절할 때)</summary>
    public void PoseAnimator(int stateHash, float normalizedTime)
    {
        if (_animator == null)
        {
            return;
        }

        if (!_isAnimatorPaused)
        {
            _isAnimatorPaused = true;
            _animatorSpeedBeforePause = _animator.speed;
        }

        _animator.speed = 0f;
        _animator.Play(stateHash, 0, normalizedTime);
    }

    /// <summary>PoseAnimator로 멈춘 애니메이터를 원래 속도로 되돌림</summary>
    public void ResumeAnimator()
    {
        if (!_isAnimatorPaused)
        {
            return;
        }

        _isAnimatorPaused = false;
        if (_animator != null)
        {
            _animator.speed = _animatorSpeedBeforePause;
        }
    }

    /// <summary>애니메이터 상태를 해시로 처음부터 다시 재생. 상태가 없으면 false</summary>
    public bool PlayStateFromStart(int stateHash)
    {
        if (_animator == null || !_animator.HasState(0, stateHash))
        {
            return false;
        }

        _animator.Play(stateHash, 0, 0f);
        return true;
    }

    /// <summary>애니메이터 상태를 이름으로 바로 재생. 상태가 없으면 false</summary>
    public bool PlayState(string stateName)
    {
        if (_animator == null || string.IsNullOrEmpty(stateName))
        {
            return false;
        }

        int hash = Animator.StringToHash(stateName);
        if (!_animator.HasState(0, hash))
        {
            return false;
        }

        _animator.Play(hash, 0, 0f);
        return true;
    }

    private static bool ContainsClip(List<AnimatorClipInfo> infos, AnimationClip clip)
    {
        for (int i = 0; i < infos.Count; i++)
        {
            if (infos[i].clip == clip)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>예약된 트리거 취소 (패턴이 끊겼을 때 공격 애니메이션이 뒤늦게 재생되지 않도록)</summary>
    /// <summary>
    /// 몸통 콜라이더의 물리 충돌을 켜고 끔 (점프 중 플레이어를 밀어내지 않도록 등).
    /// 끌 때는 트리거로 바꿔서 위치 / 크기(CenterPosition 등)는 그대로 유지
    /// </summary>
    public void SetBodyCollision(bool enabled)
    {
        if (_bodyCollider == null)
        {
            return;
        }

        if (!enabled)
        {
            if (_isBodyCollisionOff)
            {
                return;
            }

            _isBodyCollisionOff = true;
            _bodyWasTrigger = _bodyCollider.isTrigger;
            _bodyCollider.isTrigger = true;
            return;
        }

        if (!_isBodyCollisionOff)
        {
            return;
        }

        _isBodyCollisionOff = false;
        _bodyCollider.isTrigger = _bodyWasTrigger;
    }

    /// <summary>
    /// 진행 중인 애니메이션을 모두 끊고 대기 상태로 즉시 전환.
    /// 남은 트리거 / Bool을 초기화하고, 멈춰 둔 애니메이터는 다시 재생. 바로 이어서 보낸 트리거가 대기 상태에서 평가됨
    /// </summary>
    public void ForceIdleAnimation()
    {
        if (_animator == null || !_animator.isActiveAndEnabled)
        {
            return;
        }

        ResumeAnimator();

        foreach (AnimatorControllerParameter parameter in _animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger)
            {
                _animator.ResetTrigger(parameter.nameHash);
            }
            else if (parameter.type == AnimatorControllerParameterType.Bool)
            {
                _animator.SetBool(parameter.nameHash, false);
            }
        }

        if (!PlayState(_idleStateName))
        {
            Debug.LogWarning($"{name}: 대기 상태({_idleStateName})가 애니메이터에 없습니다. Idle State Name을 확인하세요.", this);
            return;
        }

        // Play는 다음 애니메이터 업데이트에 적용되므로 바로 적용 (이어서 보낸 트리거가 이전 상태에서 평가되지 않도록)
        _animator.Update(0f);
    }

    public void ResetTrigger(string parameter)
    {
        if (TryGetParameter(parameter, out int hash))
        {
            _animator.ResetTrigger(hash);
        }
    }

    public void SetBool(string parameter, bool value)
    {
        if (TryGetParameter(parameter, out int hash))
        {
            _animator.SetBool(hash, value);
        }
    }

    private bool TryGetParameter(string parameter, out int hash)
    {
        hash = 0;
        if (_animator == null || string.IsNullOrEmpty(parameter))
        {
            return false;
        }

        hash = Animator.StringToHash(parameter);
        return _animatorParameters.Contains(hash);
    }

    private void CacheAnimatorParameters()
    {
        _animatorParameters.Clear();
        if (_animator == null || _animator.runtimeAnimatorController == null)
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in _animator.parameters)
        {
            _animatorParameters.Add(parameter.nameHash);
        }
    }

    #endregion

    #region IFinishable (완전 그로기 중 플레이어 마무리 공격)

    public bool CanBeFinished => isActiveAndEnabled && CurrentState is BossFinalGroggyState;
    public Transform FinisherTransform => transform;

    public bool TryHoldForFinisher()
    {
        // 완전 그로기는 풀리지 않으므로 고정할 필요 없음
        return CanBeFinished;
    }

    public void ReleaseFinisherHold()
    {
    }

    public void ReceiveFinisher(LaunchSettings launch, float directionX)
    {
        if (!CanBeFinished)
        {
            return;
        }

        if (!_launchOnFinisher)
        {
            ChangeState(new BossDefeatedState(this));
            return;
        }

        ChangeState(new BossDefeatedState(this, _overrideLaunchSettings ? _launchSettings : launch));
    }

    #endregion

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform found = FindChildRecursive(child, childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>입 위치에서 하울링 이펙트 재생 (등장 / 페이즈 전환 공용)</summary>
    public void PlayHowlVfx()
    {
        if (EffectManager.Instance == null)
        {
            Debug.LogWarning($"{name}: EffectManager가 없어 하울링 이펙트를 재생하지 못했습니다.", this);
            return;
        }

        Log("하울링 이펙트 재생");
        EffectManager.Instance.PlayBossHowl(MouthPosition, IsFacingRight);
    }

    public void Log(string message)
    {
        if (_logState)
        {
            Debug.Log($"[Boss] {message}", this);
        }
    }
}