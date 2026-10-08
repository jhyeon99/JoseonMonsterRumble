using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 패턴 기본형. 보스(또는 자식)에 컴포넌트로 붙이면 Boss가 자동 등록.
/// - 사용 조건: 페이즈, 플레이어가 있는 층, 쿨타임 (컴포넌트를 끄면 사용 안 함)
/// - Execute() 코루틴에 패턴 흐름을 작성. 끝나면 보스는 대기 상태로 돌아감
/// - 패링 그로기 / 페이즈 전환으로 중간에 끊기면 Cleanup()이 호출됨 (판정 / 예고 끄기 등)
/// </summary>
public abstract class BossPattern : MonoBehaviour
{
    [Header("패턴 공통")]
    [SerializeField] private string _patternName;

    [Tooltip("이 패턴을 쓰는 페이즈")]
    [SerializeField]
    private BossPhaseMask _phases = BossPhaseMask.Both;

    [Tooltip("플레이어가 이 층에 있을 때만 사용")]
    [SerializeField]
    private BossFloorMask _floors = BossFloorMask.Any;

    [Tooltip("선택 확률 가중치 (클수록 자주)")]
    [SerializeField, Min(0f)]
    private float _weight = 1f;

    [Tooltip("사용 후 다시 쓸 수 있을 때까지 (초)")]
    [SerializeField, Min(0f)]
    private float _cooldown = 0f;

    private float _lastUsedTime = float.NegativeInfinity;
    private Coroutine _routine;
    private Action _onFinished;

    protected Boss Boss { get; private set; }

    public string PatternName => string.IsNullOrEmpty(_patternName) ? GetType().Name : _patternName;
    public float Weight => _weight;

    public void Initialize(Boss boss)
    {
        Boss = boss;
        OnInitialize();
    }

    protected virtual void OnInitialize()
    {
    }

    /// <summary>지금 이 패턴을 쓸 수 있는지</summary>
    public virtual bool CanUse()
    {
        if (!isActiveAndEnabled || Boss == null || _weight <= 0f)
        {
            return false;
        }

        BossPhaseMask phase = Boss.Phase >= 2 ? BossPhaseMask.Phase2 : BossPhaseMask.Phase1;
        if ((_phases & phase) == 0)
        {
            return false;
        }

        if ((_floors & BossArena.ToMask(Boss.GetPlayerFloor())) == 0)
        {
            return false;
        }

        return Time.time - _lastUsedTime >= _cooldown;
    }

    /// <summary>패턴 시작 (BossPatternState가 호출)</summary>
    public void Begin(Action onFinished)
    {
        Stop();

        _lastUsedTime = Time.time;
        _onFinished = onFinished;
        _routine = Boss.StartCoroutine(Run());
    }

    /// <summary>패턴 중단 (끝났거나 끊겼을 때). 판정 / 예고 정리</summary>
    public void Stop()
    {
        if (_routine != null)
        {
            Boss.StopCoroutine(_routine);
            _routine = null;
        }

        _onFinished = null;
        Cleanup();
    }

    private IEnumerator Run()
    {
        // 중첩 코루틴까지 이 코루틴 하나로 돌려서 Stop 한 번에 모두 멈추게 함
        IEnumerator execute = Execute();
        while (execute.MoveNext())
        {
            yield return execute.Current;
        }

        _routine = null;
        Cleanup();

        Action onFinished = _onFinished;
        _onFinished = null;
        onFinished?.Invoke();
    }

    /// <summary>패턴 흐름</summary>
    protected abstract IEnumerator Execute();

    /// <summary>끝났거나 끊겼을 때 정리 (판정 끄기, 예고 끄기 등)</summary>
    protected virtual void Cleanup()
    {
    }

    /// <summary>인스펙터에서 컴포넌트를 처음 붙였을 때 기본값 지정용</summary>
    protected void SetDefaults(string patternName, BossPhaseMask phases, BossFloorMask floors)
    {
        _patternName = patternName;
        _phases = phases;
        _floors = floors;
    }
}
