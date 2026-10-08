using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 패턴 기본형. 보스(또는 자식)에 컴포넌트로 붙이면 Boss가 자동 등록.
/// - 패턴 하나 = 애니메이션 하나. 시작할 때 트리거 하나를 보내고, 애니메이션 길이만큼 패턴이 진행됨
/// - 판정 시작 / 끝은 애니메이션 길이 대비 비율 (플레이어 공격의 HitStartRatio / HitEndRatio와 같은 방식)
/// - 사용 조건: 페이즈, 플레이어가 있는 층, 쿨타임 (컴포넌트를 끄면 사용 안 함)
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

    [Header("애니메이션")]
    [Tooltip("패턴 시작 시 보낼 애니메이터 트리거 (애니메이터에 없으면 무시)")]
    [SerializeField]
    private string _animationTrigger;

    [Tooltip("패턴 애니메이션 클립. 이 길이가 패턴 전체 길이가 됨")]
    [SerializeField]
    private AnimationClip _animationClip;

    [Tooltip("클립이 없을 때 패턴 전체 길이 (초)")]
    [SerializeField, Min(0.05f)]
    private float _fallbackDuration = 1.5f;

    [Tooltip("애니메이터 상태의 Speed 값과 맞추기 (2면 클립이 두 배 빠르게 재생)")]
    [SerializeField, Min(0.01f)]
    private float _animationSpeed = 1f;

    [Tooltip("트리거를 보낸 뒤 애니메이터가 이 클립 재생을 시작할 때까지 기다리는 최대 시간 (초). 이전 애니메이션이 끝나기를 기다려야 할 때")]
    [SerializeField, Min(0f)]
    private float _animationStartTimeout = 1.5f;

    [Tooltip("애니메이션이 끝난 뒤 다음 행동까지 추가 대기 (초)")]
    [SerializeField, Min(0f)]
    private float _afterDelay = 0f;

    [Header("판정 구간 (애니메이션 길이 대비 비율)")]
    [Tooltip("판정 시작 지점 (0 ~ 1)")]
    [SerializeField, Range(0f, 1f)]
    private float _hitStartRatio = 0.4f;

    [Tooltip("판정 끝 지점 (0 ~ 1)")]
    [SerializeField, Range(0f, 1f)]
    private float _hitEndRatio = 0.6f;

    private float _lastUsedTime = float.NegativeInfinity;
    private float _startTime;
    private Coroutine _routine;
    private Action _onFinished;

    protected Boss Boss { get; private set; }

    public string PatternName => string.IsNullOrEmpty(_patternName) ? GetType().Name : _patternName;
    public float Weight => _weight;

    /// <summary>패턴 전체 길이 (초) = 클립 길이 / 재생 속도</summary>
    public float Duration => _animationClip != null ? _animationClip.length / _animationSpeed : _fallbackDuration;

    protected float HitStartRatio => _hitStartRatio;
    protected float HitEndRatio => Mathf.Max(_hitStartRatio, _hitEndRatio);

    /// <summary>패턴 진행도 (0 ~ 1, 애니메이션 길이 기준)</summary>
    protected float Progress => Duration > 0f ? Mathf.Clamp01((Time.time - _startTime) / Duration) : 1f;

    /// <summary>비율을 초로 변환</summary>
    protected float RatioToSeconds(float ratio)
    {
        return ratio * Duration;
    }

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

        if (Boss != null)
        {
            Boss.ResetTrigger(_animationTrigger);
        }

        Cleanup();
    }

    private IEnumerator Run()
    {
        _startTime = Time.time;
        OnPatternStart();
        Boss.SetTrigger(_animationTrigger);

        // 애니메이터가 실제로 이 클립을 재생하기 시작한 순간부터 진행도를 셈
        // (이전 애니메이션이 끝나기를 기다리는 동안 판정 / 발사가 먼저 나가지 않도록)
        if (_animationClip != null && Boss.HasAnimator)
        {
            float waitUntil = Time.time + _animationStartTimeout;
            while (!Boss.IsPlayingClip(_animationClip) && Time.time < waitUntil)
            {
                yield return null;
            }

            if (!Boss.IsPlayingClip(_animationClip))
            {
                Debug.LogWarning($"{name}: {PatternName} 클립({_animationClip.name})이 재생되지 않아 트리거 기준으로 진행합니다. 트리거 이름 / 애니메이터 전이를 확인하세요.", this);
            }

            _startTime = Time.time;
        }

        // 중첩된 IEnumerator(RunHitWindow, WaitUntilProgress 등)까지 이 코루틴 하나에서 직접 돌림.
        // Unity에 중첩 코루틴으로 넘기면 Stop(그로기 등) 후에도 안쪽이 계속 돌아 판정이 켜질 수 있음
        var stack = new Stack<IEnumerator>();
        stack.Push(Execute());

        while (stack.Count > 0)
        {
            IEnumerator current = stack.Peek();
            if (!current.MoveNext())
            {
                stack.Pop();
                continue;
            }

            if (current.Current is IEnumerator nested)
            {
                stack.Push(nested);
                continue;
            }

            yield return current.Current;
        }

        // 애니메이션이 끝날 때까지 기다림
        while (Progress < 1f)
        {
            yield return null;
        }

        if (_afterDelay > 0f)
        {
            yield return new WaitForSeconds(_afterDelay);
        }

        _routine = null;
        Cleanup();

        Action onFinished = _onFinished;
        _onFinished = null;
        onFinished?.Invoke();
    }

    /// <summary>트리거를 보내기 직전 (방향 전환 등)</summary>
    protected virtual void OnPatternStart()
    {
        Boss.FacePlayer();
    }

    /// <summary>패턴 흐름. 애니메이션 시작부터 진행도(Progress)를 보고 판정 / 발사 등을 처리</summary>
    protected abstract IEnumerator Execute();

    /// <summary>끝났거나 끊겼을 때 정리 (판정 끄기, 예고 끄기 등)</summary>
    protected virtual void Cleanup()
    {
    }

    /// <summary>진행도가 ratio가 될 때까지 대기</summary>
    protected IEnumerator WaitUntilProgress(float ratio)
    {
        while (Progress < ratio)
        {
            yield return null;
        }
    }

    /// <summary>판정 구간 처리: 시작 전까지 예고 → 시작~끝 판정 → 끄기</summary>
    protected IEnumerator RunHitWindow(BossHitbox hitbox, bool showTelegraph = true)
    {
        if (showTelegraph)
        {
            hitbox.SetTelegraph(true);
        }

        yield return WaitUntilProgress(HitStartRatio);

        hitbox.SetTelegraph(false);
        hitbox.Activate();

        yield return WaitUntilProgress(HitEndRatio);

        hitbox.Deactivate();
    }

    /// <summary>인스펙터에서 컴포넌트를 처음 붙였을 때 기본값 지정용</summary>
    protected void SetDefaults(string patternName, BossPhaseMask phases, BossFloorMask floors)
    {
        _patternName = patternName;
        _phases = phases;
        _floors = floors;
    }

    protected virtual void OnValidate()
    {
        if (_hitEndRatio < _hitStartRatio)
        {
            _hitEndRatio = _hitStartRatio;
        }
    }
}
