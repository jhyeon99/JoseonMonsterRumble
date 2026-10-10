using System.Collections;
using UnityEngine;

/// <summary>
/// 소환 (체력 비율 발동). 현재 페이즈 체력이 지정한 비율 이하가 되면 다른 패턴보다 먼저 무조건 사용.
/// - 시작하면 패턴 트리거(angry 등)를 보내고, 애니메이션의 소환 비율 지점부터 일정 간격으로 맵 아래에서 독 방울이 n개씩 올라옴
/// - 방울은 천천히 계속 올라가다 끝 높이에서 사라짐. 터트리면 올챙이가 나옴 (3타로 날려 보스에게 기믹 데미지)
/// - 패턴 중 보스는 제자리에 있고, 직접 공격은 받지 않음 (올챙이 기믹 데미지만 들어감)
/// - 최대 시간이 지나거나, 패턴 중 깎인 체력이 최대 체력의 일정 비율 이상이면 종료. 종료 시 방울 / 올챙이 모두 제거
/// - 반복 클립(angry_loop)이 있으면 패턴이 끝날 때까지 반복 재생하고, 끝나면 종료 트리거(angryEnd)로 마무리 애니메이션 후 종료
///   반복 클립이 없으면 패턴 길이(Duration Override)가 지나면 대기(Idle) 상태로 돌아감
///   (판정 시작 / 끝 비율은 사용하지 않음)
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

    [Header("독 방울")]
    [SerializeField]
    private BossPoisonBubble _bubblePrefab;

    [Tooltip("반복 클립이 없을 때: 첫 방울이 올라오는 지점 (애니메이션 길이 대비 비율). 반복 클립이 있으면 반복 시작 + Spawn Delay After Loop")]
    [SerializeField, Range(0f, 1f)]
    private float _spawnRatio = 0.6f;

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

    [Header("종료 조건 (둘 중 하나라도 만족하면 종료)")]
    [Tooltip("패턴 최대 시간 (초, 애니메이션 시작부터)")]
    [SerializeField, Min(0.1f)]
    private float _maxDuration = 20f;

    [Tooltip("패턴 중 깎인 체력이 현재 페이즈 최대 체력의 이 비율 이상이면 종료 (0이면 사용 안 함)")]
    [SerializeField, Range(0f, 1f)]
    private float _endDamageRatio = 0.15f;

    [Header("애니메이션 반복 (angry 유지)")]
    [Tooltip("패턴이 끝날 때까지 반복할 클립 (angry_loop). 비우면 이어지는 클립 중 반복(Loop Time) 클립을 자동으로 사용")]
    [SerializeField]
    private AnimationClip _loopClip;

    [Tooltip("패턴이 끝날 때 반복을 끝내는 애니메이터 트리거 (angry_loop → angry_end)")]
    [SerializeField]
    private string _loopEndTrigger = "angryEnd";

    [Tooltip("반복 클립이 재생되기 시작한 뒤 첫 방울이 올라오기까지 (초)")]
    [SerializeField, Min(0f)]
    private float _spawnDelayAfterLoop = 0f;

    [Header("보스")]
    [Tooltip("반복 클립이 없을 때: 패턴 길이가 지나면 재생할 애니메이터 상태 이름")]
    [SerializeField]
    private string _idleState = "Idle";

    [Tooltip("패턴 중 보스가 플레이어의 직접 공격을 받지 않음 (올챙이 기믹 데미지만)")]
    [SerializeField]
    private bool _blockDirectDamage = true;

    private bool[] _usedPhase1;
    private bool[] _usedPhase2;
    private bool _pending;
    private bool _isRunning;
    private float _startHp;

    public override bool IsForced => true;
    public override bool BlocksDirectDamage => _blockDirectDamage && _isRunning;

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
        _startHp = Boss.Health.CurrentHp;
    }

    private AnimationClip LoopClip => _loopClip != null ? _loopClip : FindLoopingFollowingClip();

    /// <summary>반복 클립으로 유지하면 패턴 길이를 기다리지 않음 (종료 조건으로 끝을 판단)</summary>
    protected override bool WaitForDurationAfterExecute => LoopClip == null;

    protected override IEnumerator Execute()
    {
        AnimationClip loopClip = LoopClip;
        if (loopClip != null && Boss.HasAnimator)
        {
            yield return ExecuteWithLoop(loopClip);
            yield break;
        }

        yield return ExecuteWithoutLoop();
    }

    /// <summary>angry_start → angry_loop(종료 조건까지 유지) → angryEnd → angry_end → 종료</summary>
    private IEnumerator ExecuteWithLoop(AnimationClip loopClip)
    {
        float startTime = Time.time;

        // 반복 구간에 들어갈 때까지 대기 (안 들어가면 시작 클립 길이 정도 뒤 그대로 진행)
        float waitUntil = Time.time + Mathf.Max(1f, Duration);
        while (!Boss.IsPlayingClip(loopClip) && Time.time < waitUntil)
        {
            yield return null;
        }

        if (_spawnDelayAfterLoop > 0f)
        {
            yield return new WaitForSeconds(_spawnDelayAfterLoop);
        }

        float nextWave = Time.time;
        while (!ShouldEnd(startTime))
        {
            if (Time.time >= nextWave)
            {
                SpawnWave();
                nextWave = Time.time + _waveInterval;
            }

            yield return null;
        }

        // 반복 끝 → 마무리 애니메이션이 끝날 때까지 대기 (이후 애니메이터 전이로 Idle)
        Boss.SetTrigger(_loopEndTrigger);

        float endTimeout = Time.time + 3f;
        while (Boss.IsPlayingClip(loopClip) && Time.time < endTimeout)
        {
            yield return null;
        }

        while (IsPlayingPatternClip() && Time.time < endTimeout)
        {
            yield return null;
        }

        Boss.ResetTrigger(_loopEndTrigger);
    }

    private IEnumerator ExecuteWithoutLoop()
    {
        float startTime = Time.time;

        yield return WaitUntilProgress(_spawnRatio);

        float nextWave = Time.time;
        bool returnedToIdle = false;
        while (!ShouldEnd(startTime))
        {
            if (Time.time >= nextWave)
            {
                SpawnWave();
                nextWave = Time.time + _waveInterval;
            }

            if (!returnedToIdle && Progress >= 1f)
            {
                ReturnToIdle();
                returnedToIdle = true;
            }

            yield return null;
        }

        // 종료 조건이 angry보다 먼저 만족되면 angry가 끝날 때까지 기다린 뒤 대기로
        if (!returnedToIdle)
        {
            yield return WaitUntilProgress(1f);
            ReturnToIdle();
        }
    }

    /// <summary>angry 애니메이션이 아직 재생 중이면 대기 상태로 (이미 넘어갔으면 그대로)</summary>
    private void ReturnToIdle()
    {
        if (!IsPlayingPatternClip())
        {
            return;
        }

        if (!Boss.PlayState(_idleState))
        {
            Debug.LogWarning($"{name}: {PatternName} 종료 후 재생할 상태({_idleState})가 애니메이터에 없습니다. Idle State 이름을 확인하세요.",
                this);
        }
    }

    private bool ShouldEnd(float startTime)
    {
        if (Time.time - startTime >= _maxDuration)
        {
            return true;
        }

        if (_endDamageRatio > 0f && Boss.Health.MaxHp > 0f)
        {
            float damaged = _startHp - Boss.Health.CurrentHp;
            if (damaged / Boss.Health.MaxHp >= _endDamageRatio)
            {
                return true;
            }
        }

        return false;
    }

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
        // 끝났거나 끊겼으면 남은 방울 / 올챙이 모두 제거
        if (_isRunning && Boss != null)
        {
            Boss.ClearMinions();
        }

        _isRunning = false;
    }

    private void OnDrawGizmosSelected()
    {
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