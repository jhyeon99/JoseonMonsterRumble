using System.Collections;
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

    [Tooltip("점프할 때 재생할 애니메이터 트리거 (비우면 애니메이션 변경 없음). 착지하면 대기 상태(Idle State)로 바로 돌려서 angry가 곧바로 시작되게 함")]
    [SerializeField]
    private string _jumpTrigger = "";

    [Tooltip("뛰어오를 때 발밑 먼지 (크기 / 위치는 Boss의 점프 먼지 설정)")]
    [SerializeField]
    private bool _takeoffDust = true;

    [Tooltip("착지할 때 발밑 먼지")]
    [SerializeField]
    private bool _landingDust = true;

    [Tooltip("소환 위치까지 점프하는 시간 (초)")]
    [SerializeField, Min(0.05f)]
    private float _jumpDuration = 0.8f;

    [Tooltip("출발 / 도착 중 높은 쪽 기준으로 더 올라가는 높이")]
    [SerializeField, Min(0f)]
    private float _jumpHeight = 3f;

    [Tooltip("점프하는 동안 몸통 충돌(플레이어 밀어내기)과 접촉 데미지를 끔")]
    [SerializeField]
    private bool _disableCollisionWhileJumping = true;

    [Tooltip("착지 후 angry 시작까지 대기 (초)")]
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
    private float _maxDuration = 20f;

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

    [Tooltip("실패(즉사) 시 보낼 트리거 (차지 → 해방 애니메이션)")]
    [SerializeField]
    private string _releaseTrigger = "ultimateRelease";

    [Tooltip("파훼 시 재생할 피격 애니메이터 상태 이름")]
    [SerializeField]
    private string _hitStateName = "Boss_Hit";

    [Tooltip("피격 애니메이션 클립 (길이만큼 기다린 뒤 복귀 패턴). 비우면 Hit Fallback Duration")]
    [SerializeField]
    private AnimationClip _hitClip;

    [SerializeField, Min(0f)]
    private float _hitFallbackDuration = 1f;

    [Header("궁극기 실패 (즉사)")]
    [Tooltip("즉사 연출 프리팹 (PF_BossDeathSpike). 플레이어 위치에 생성")]
    [SerializeField]
    private GameObject _deathVfxPrefab;

    [Tooltip("즉사 연출 위치 보정 (플레이어 발밑 기준)")]
    [SerializeField]
    private Vector2 _deathVfxOffset = Vector2.zero;

    [Tooltip("즉사 연출이 사라지기까지 (초)")]
    [SerializeField, Min(0.1f)]
    private float _deathVfxLifetime = 3f;

    [Tooltip("즉사 연출 생성 후 플레이어가 죽기까지 (초). 가시가 솟는 타이밍에 맞추기")]
    [SerializeField, Min(0f)]
    private float _killDelay = 0.3f;

    [Tooltip("사망 원인 표시 이름")]
    [SerializeField]
    private string _killReason = "궁극기 (가시 즉사)";

    [Tooltip("즉사 시 화면을 덮는 색 (알파 = 최대 진하기). 알파 0이면 사용 안 함")]
    [SerializeField]
    private Color _deathScreenColor = new Color(0.8f, 0f, 0f, 0.55f);

    [Tooltip("붉은 화면이 차오르는 시간 (초)")]
    [SerializeField, Min(0.01f)]
    private float _deathScreenFadeIn = 0.15f;

    [Tooltip("붉은 화면이 빠지는 시간 (초)")]
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

    public override bool IsForced => true;
    public override bool BlocksDirectDamage => _blockDirectDamage && _isRunning;

    /// <summary>점프 중에는 접촉 데미지 끔</summary>
    public override bool DisablesContactDamage =>
        base.DisablesContactDamage || (_isJumping && _disableCollisionWhileJumping);

    private void Reset()
    {
        SetDefaults("소환", BossPhaseMask.Both, BossFloorMask.Any);
    }

    protected override void OnInitialize()
    {
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

    private void HandlePhaseChanged(int phase)
    {
        // 새 페이즈는 체력이 가득 찬 상태로 시작하므로 비율 체크를 처음부터
        _usedPhase2 = new bool[_phase2HpRatios != null ? _phase2HpRatios.Length : 0];
        _pending = false;
    }

    private void HandleHpChanged(float current, float max)
    {
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

        Boss.FaceDirection(target.x - start.x);
        if (!string.IsNullOrEmpty(_jumpTrigger))
        {
            Boss.SetTrigger(_jumpTrigger);
        }

        SetJumping(true);

        // 이륙 먼지 (Boss의 Takeoff Dust Timing만큼 늦게. 음수는 시작 순간)
        if (_takeoffDust)
        {
            Boss.PlayJumpDust(start.x, start.y, Boss.TakeoffDustTiming);
        }

        float landingTiming = Boss.LandingDustTiming;
        bool landingDustPlayed = !_landingDust;

        float apexY = Mathf.Max(start.y, target.y) + _jumpHeight;
        float elapsed = 0f;
        while (elapsed < _jumpDuration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / _jumpDuration);

            float x = Mathf.Lerp(start.x, target.x, Mathf.SmoothStep(0f, 1f, t));
            Boss.MoveTo(new Vector2(x, GetJumpY(t, start.y, apexY, target.y)));

            // 착지 직전에 미리 (Landing Dust Timing이 음수)
            if (!landingDustPlayed && landingTiming < 0f && _jumpDuration - elapsed <= -landingTiming)
            {
                Boss.PlayJumpDust(target.x, target.y);
                landingDustPlayed = true;
            }

            yield return new WaitForFixedUpdate();
        }

        Boss.MoveTo(target);
        yield return new WaitForFixedUpdate();

        if (!landingDustPlayed)
        {
            Boss.PlayJumpDust(target.x, target.y, landingTiming);
        }

        SetJumping(false);

        // ultimate 전이는 대기(Boss_Idle)에서만 있으므로, 점프 애니메이션이 끝나기를 기다리지 않고 바로 대기로
        Boss.ForceIdleAnimation();

        CameraShake.Shake(_landingShake);
        Boss.FacePlayer();

        if (_delayAfterLanding > 0f)
        {
            yield return new WaitForSeconds(_delayAfterLanding);
        }
    }

    private void SetJumping(bool jumping)
    {
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
        float deadline = startTime + _maxDuration;

        _gimmickHits = 0;
        BossBaby.CountHitsOnly = true;
        BossBaby.OnHitBoss -= HandleGimmickHit;
        BossBaby.OnHitBoss += HandleGimmickHit;
        SetChargeProgress(0f);

        float nextWave = Time.time + _spawnDelayAfterLoop;
        float chargeStart = -1f;
        bool success = false;

        while (Time.time < deadline)
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
                Boss.Log($"{PatternName}: 차지 시작 (남은 시간 {deadline - chargeStart:0.0}초)");
            }

            if (chargeStart >= 0f)
            {
                float span = Mathf.Max(0.01f, deadline - chargeStart);
                SetChargeProgress((Time.time - chargeStart) / span);
            }

            yield return null;
        }

        BossBaby.OnHitBoss -= HandleGimmickHit;
        BossBaby.CountHitsOnly = false;
        Boss.ClearMinions();

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
        SetChargeProgress(1f);
        Boss.SetTrigger(_releaseTrigger);

        Transform player = Boss.Player;
        if (_deathVfxPrefab != null && player != null)
        {
            Vector3 position = player.position + (Vector3)_deathVfxOffset;
            GameObject vfx = Instantiate(_deathVfxPrefab, position, Quaternion.identity);
            Destroy(vfx, _deathVfxLifetime);
        }

        CameraShake.Shake(_deathShake);
        if (_deathScreenColor.a > 0f)
        {
            Boss.StartCoroutine(DeathScreenRoutine());
        }

        if (_killDelay > 0f)
        {
            yield return new WaitForSeconds(_killDelay);
        }

        PlayerHealth playerHealth = player != null ? player.GetComponentInParent<PlayerHealth>() : null;
        if (playerHealth != null)
        {
            playerHealth.Kill(_killReason);
        }

        // 해방 애니메이션이 끝날 때까지 (이후 애니메이터 전이로 대기)
        yield return new WaitForSeconds(1f);
        Boss.ResetTrigger(_releaseTrigger);
    }

    /// <summary>애니메이터가 차지 상태인지 (상태 이름 → 클립 순으로 확인, 둘 다 없으면 바로 true)</summary>
    private bool IsInChargeState()
    {
        Animator animator = Boss.GetComponentInChildren<Animator>();
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

    private void SetChargeProgress(float value)
    {
        if (string.IsNullOrEmpty(_chargeProgressParameter) || Boss == null || !Boss.HasAnimator)
        {
            return;
        }

        Animator animator = Boss.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Float && parameter.name == _chargeProgressParameter)
            {
                animator.SetFloat(parameter.nameHash, Mathf.Clamp01(value));
                return;
            }
        }
    }

    /// <summary>화면 전체를 붉게 덮었다가 빠짐 (런타임에 오버레이 생성)</summary>
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
        float maxAlpha = color.a;

        for (float t = 0f; t < _deathScreenFadeIn && screen != null; t += Time.unscaledDeltaTime)
        {
            color.a = Mathf.Lerp(0f, maxAlpha, t / _deathScreenFadeIn);
            screen.color = color;
            yield return null;
        }

        for (float t = 0f; t < _deathScreenFadeOut && screen != null; t += Time.unscaledDeltaTime)
        {
            color.a = Mathf.Lerp(maxAlpha, 0f, t / _deathScreenFadeOut);
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

        float[] placed = new float[_bubblesPerWave];
        int placedCount = 0;

        for (int i = 0; i < _bubblesPerWave; i++)
        {
            // 같은 웨이브끼리 너무 붙지 않게 몇 번 다시 뽑아 봄
            float x = Random.Range(left, right);
            for (int attempt = 0; attempt < 8 && IsTooClose(x, placed, placedCount); attempt++)
            {
                x = Random.Range(left, right);
            }

            placed[placedCount++] = x;

            BossPoisonBubble bubble = Instantiate(_bubblePrefab, new Vector3(x, spawnY, 0f), Quaternion.identity);
            bubble.Initialize(Boss, topY);
        }
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

    protected override void Cleanup()
    {
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

            if (_returnPattern != null)
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