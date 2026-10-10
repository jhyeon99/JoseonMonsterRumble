using UnityEngine;

public enum FlyingMeleeAttackPhase
{
    Positioning,
    Hovering,
    Preparing,
    Striking,
    Recovering,
    Completed,
}

/// <summary>공격 진행과 명중 감속을 한 시간 기준으로 계산한다. 애니메이션은 읽지 않는다.</summary>
public sealed class FlyingMeleeAttackTimeline
{
    // 준비 구간은 조준점을 통과하는 경로 중간 이전에 끝난다. 돌진 시간으로 이 위치가 바뀌지 않는다.
    private const float PreparationEndProgress = 0.4f;
    private readonly FlyingMeleeAttackTiming _timing;
    private readonly float _positioningTimeout;
    private readonly float _hoverTime;
    private readonly float _hitSpeedRatio;
    private readonly float _hitSlowTime;
    private float _elapsedTime;
    private float _hitSlowRemaining;

    public FlyingMeleeAttackPhase Phase { get; private set; }
    public float StrikeProgress => Mathf.Clamp01(_elapsedTime / _timing.StrikeTime);
    public float PrepareProgress => Mathf.Clamp01(_elapsedTime / _timing.PrepareTime);
    public float TravelProgress => Phase switch
    {
        FlyingMeleeAttackPhase.Preparing => PreparationEndProgress * PrepareProgress,
        FlyingMeleeAttackPhase.Striking => PreparationEndProgress + (1f - PreparationEndProgress) * StrikeProgress,
        FlyingMeleeAttackPhase.Recovering or FlyingMeleeAttackPhase.Completed => 1f,
        _ => 0f,
    };
    public bool HasPositioningTimedOut => _elapsedTime >= _positioningTimeout;

    public FlyingMeleeAttackTimeline(FlyingMeleeAttackData data, FlyingMeleeAttackPattern pattern)
    {
        _timing = data.GetTiming(pattern);
        _positioningTimeout = data.PositioningTimeout;
        _hoverTime = data.HoverTime;
        _hitSpeedRatio = data.HitSpeedRatio;
        _hitSlowTime = data.HitSlowTime;
        SetPhase(FlyingMeleeAttackPhase.Positioning);
    }

    public void SetPhase(FlyingMeleeAttackPhase phase)
    {
        Phase = phase;
        _elapsedTime = 0f;
        _hitSlowRemaining = 0f;
    }

    public void ApplyHitSlowdown()
    {
        if (Phase == FlyingMeleeAttackPhase.Striking)
        {
            _hitSlowRemaining = _hitSlowTime;
        }
    }

    // 위치 확보의 도착 조건은 이동 담당자가 판단한다.
    public bool Advance(float deltaTime)
    {
        if (Phase == FlyingMeleeAttackPhase.Completed || deltaTime <= 0f)
        {
            return false;
        }

        float slowedTime = Mathf.Min(deltaTime, _hitSlowRemaining);
        _hitSlowRemaining -= slowedTime;
        _elapsedTime += deltaTime - slowedTime + slowedTime * _hitSpeedRatio;

        return Phase switch
        {
            FlyingMeleeAttackPhase.Hovering => _elapsedTime >= _hoverTime,
            FlyingMeleeAttackPhase.Preparing => _elapsedTime >= _timing.PrepareTime,
            FlyingMeleeAttackPhase.Striking => _elapsedTime >= _timing.StrikeTime,
            FlyingMeleeAttackPhase.Recovering => _elapsedTime >= _timing.RecoveryTime,
            _ => false,
        };
    }
}
