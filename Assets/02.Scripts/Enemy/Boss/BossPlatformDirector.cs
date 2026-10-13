using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스전 플랫폼 배치 전환.
/// - 기본 배치: 아래 두 경우 외 모두 (대기 / 다른 패턴 / 그로기 / 연출 등)
/// - 찍기 배치: 찍기 패턴(BossStompPattern) 중에만. 패턴이 시작되면 기본 플랫폼만 사라지고(패턴은 기다리지 않음),
///   마지막 찍기가 땅에 닿는 순간(NotifyStompLanded) 찍기 플랫폼이 자라남
/// - 소환 배치: 소환 패턴(BossSummonPattern) 중에만
/// 전환 순서: 빠지는 플랫폼을 자라나는 애니메이션 역재생으로 없앤 뒤 → 새 플랫폼을 자라나는 애니메이션으로 생성.
/// 두 배치에 모두 들어 있는 플랫폼은 그대로 유지.
/// 소환 패턴은 해당 배치가 다 생긴 뒤에 시작 (BossPattern이 PrepareFor로 기다림)
/// </summary>
public class BossPlatformDirector : MonoBehaviour
{
    public enum Layout
    {
        Default,
        Stomp,
        Summon,

        /// <summary>찍기 패턴 시작 ~ 마지막 착지 전: 찍기 배치에 없는 플랫폼만 없애고 새로 자라나지는 않음</summary>
        StompWaiting,
    }

    [Header("배치")]
    [Tooltip("기본 배치 플랫폼")]
    [SerializeField]
    private BossPlatform[] _defaultPlatforms;

    [Tooltip("찍기 패턴 중에만 있는 플랫폼")]
    [SerializeField]
    private BossPlatform[] _stompPlatforms;

    [Tooltip("소환 패턴 중에만 있는 플랫폼")]
    [SerializeField]
    private BossPlatform[] _summonPlatforms;

    [Header("전환")]
    [Tooltip("자라나기 / 사라지기 애니메이션 재생 속도 배율 (2면 두 배 빠르게)")]
    [SerializeField, Min(0.1f)]
    private float _transitionSpeed = 1.5f;

    [Tooltip("사라진 뒤 새 플랫폼이 자라나기 시작하기까지 (초)")]
    [SerializeField, Min(0f)]
    private float _gapBetween = 0.1f;

    [Tooltip("패턴이 배치 전환을 기다리는 최대 시간 (초). 넘으면 그냥 시작")]
    [SerializeField, Min(0.1f)]
    private float _maxPatternWait = 6f;

    [Tooltip("켜면 처음 시작할 때 기본 배치가 자라나는 애니메이션으로 생김. 끄면 처음부터 다 자란 상태")]
    [SerializeField]
    private bool _growDefaultOnStart = true;

    [Tooltip("비어 있으면 씬에서 자동으로 찾음")]
    [SerializeField]
    private Boss _boss;

    private readonly HashSet<BossPlatform> _all = new HashSet<BossPlatform>();
    private Layout _target = Layout.Default;
    private Layout _current = Layout.Default;
    private Coroutine _transition;

    // 이번 찍기 패턴에서 마지막 찍기가 땅에 닿았는지
    private bool _stompLanded;
    private BossPattern _stompPattern;
    private bool _stompPrepared;

    public float MaxPatternWait => _maxPatternWait;

    /// <summary>지금 화면의 배치가 target과 같고 전환 중이 아닌지</summary>
    public bool IsReady(Layout layout) => _transition == null && _current == layout && _target == layout;

    private void Awake()
    {
        Register(_defaultPlatforms);
        Register(_stompPlatforms);
        Register(_summonPlatforms);
    }

    private void Start()
    {
        if (_boss == null)
        {
            _boss = FindFirstObjectByType<Boss>();
        }

        _target = Layout.Default;

        if (_growDefaultOnStart)
        {
            // 전부 없는 상태에서 기본 배치가 자라나며 시작
            foreach (BossPlatform platform in _all)
            {
                platform.SetShownImmediate(false);
            }

            _current = Layout.Stomp;
            _transition = StartCoroutine(TransitionRoutine());
            return;
        }

        // 애니메이션 없이 바로 기본 배치
        foreach (BossPlatform platform in _all)
        {
            platform.SetShownImmediate(Contains(_defaultPlatforms, platform));
        }

        _current = Layout.Default;
    }

    private void Update()
    {
        // 패턴이 끝나거나 끊기면(대기 / 그로기 / 연출 등) 기본 배치로
        if (_boss != null)
        {
            BossPattern pattern = _boss.CurrentState is BossPatternState patternState ? patternState.Pattern : null;
            RequestLayout(ResolveLayout(pattern));
        }
    }

    /// <summary>
    /// 패턴 시작 전 호출. 이 패턴에 맞는 배치로 전환을 요청하고, 다 바뀌었으면 true (BossPattern이 true가 될 때까지 기다림)
    /// </summary>
    public bool PrepareFor(BossPattern pattern)
    {
        // 찍기 패턴이 새로 시작될 때마다 착지 기록 초기화 (같은 찍기 패턴이 연달아 와도)
        if (GetLayout(pattern) == Layout.Stomp && _stompLanded && _boss != null && _boss.CurrentState is BossPatternState state && state.Pattern == pattern && !_stompPrepared)
        {
            _stompLanded = false;
        }

        _stompPrepared = GetLayout(pattern) == Layout.Stomp;
        Layout layout = ResolveLayout(pattern);
        RequestLayout(layout);

        // 찍기는 기다리지 않고 바로 시작 (찍기 플랫폼은 마지막 착지 때 자라남)
        return layout == Layout.StompWaiting || IsReady(layout);
    }

    /// <summary>찍기 패턴의 마지막 찍기가 땅에 닿음 → 찍기 플랫폼 자라나기</summary>
    public void NotifyStompLanded()
    {
        _stompLanded = true;
        RequestLayout(Layout.Stomp);
    }

    /// <summary>패턴에 맞는 배치. 찍기는 마지막 착지 전이면 StompWaiting, 후면 Stomp. 새 찍기 패턴이면 착지 기록 초기화</summary>
    private Layout ResolveLayout(BossPattern pattern)
    {
        Layout layout = GetLayout(pattern);
        if (layout != Layout.Stomp)
        {
            _stompLanded = false;
            _stompPattern = null;
            return layout;
        }

        if (_stompPattern != pattern)
        {
            _stompPattern = pattern;
            _stompLanded = false;
        }

        return _stompLanded ? Layout.Stomp : Layout.StompWaiting;
    }

    public static Layout GetLayout(BossPattern pattern)
    {
        if (pattern is BossStompPattern)
        {
            return Layout.Stomp;
        }

        if (pattern is BossSummonPattern)
        {
            return Layout.Summon;
        }

        return Layout.Default;
    }

    public void RequestLayout(Layout layout)
    {
        if (_target == layout && (_transition != null || _current == layout))
        {
            return;
        }

        _target = layout;
        if (_transition == null)
        {
            _transition = StartCoroutine(TransitionRoutine());
        }
    }

    /// <summary>목표 배치가 될 때까지: 빠지는 플랫폼 역재생 → (잠깐) → 새 플랫폼 자라나기. 도중에 목표가 바뀌면 이어서 다시 맞춤</summary>
    private IEnumerator TransitionRoutine()
    {
        while (true)
        {
            Layout goal = _target;
            BossPlatform[] goalSet = GetSet(goal);

            // 1. 빠지는 플랫폼 사라지기
            bool anyShrink = false;
            foreach (BossPlatform platform in _all)
            {
                if (!Contains(goalSet, platform) && !platform.IsHidden)
                {
                    platform.Shrink(_transitionSpeed);
                    anyShrink = true;
                }
            }

            while (AnyAnimating())
            {
                yield return null;
            }

            if (anyShrink && _gapBetween > 0f)
            {
                yield return new WaitForSeconds(_gapBetween);
            }

            // 기다리는 동안 목표가 바뀌었으면 처음부터 다시
            if (_target != goal)
            {
                continue;
            }

            // 찍기 대기: 없앨 것만 없애고 새로 자라나지는 않음 (마지막 착지 때 Stomp로 바뀌며 자라남)
            if (goal == Layout.StompWaiting)
            {
                _current = goal;
                break;
            }

            // 2. 새 플랫폼 자라나기
            foreach (BossPlatform platform in goalSet)
            {
                if (platform != null && !platform.IsShown)
                {
                    platform.Grow(_transitionSpeed);
                }
            }

            while (AnyAnimating())
            {
                yield return null;
            }

            if (_target == goal)
            {
                _current = goal;
                break;
            }
        }

        _transition = null;
    }

    private bool AnyAnimating()
    {
        foreach (BossPlatform platform in _all)
        {
            if (platform.IsAnimating)
            {
                return true;
            }
        }

        return false;
    }

    private BossPlatform[] GetSet(Layout layout)
    {
        switch (layout)
        {
            case Layout.Stomp:
            case Layout.StompWaiting:
                return _stompPlatforms;
            case Layout.Summon:
                return _summonPlatforms;
            default:
                return _defaultPlatforms;
        }
    }

    private void Register(BossPlatform[] platforms)
    {
        if (platforms == null)
        {
            return;
        }

        foreach (BossPlatform platform in platforms)
        {
            if (platform != null)
            {
                _all.Add(platform);
            }
        }
    }

    private static bool Contains(BossPlatform[] set, BossPlatform platform)
    {
        if (set == null)
        {
            return false;
        }

        foreach (BossPlatform item in set)
        {
            if (item == platform)
            {
                return true;
            }
        }

        return false;
    }
}
