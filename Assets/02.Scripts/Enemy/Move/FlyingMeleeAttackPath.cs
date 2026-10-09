using UnityEngine;

/// <summary>확정한 시작점·조준점·종료점으로 궤적을 계산한다. 애니메이션과 현재 표적 위치는 읽지 않는다.</summary>
public readonly struct FlyingMeleeAttackPath
{
    private const float HalfProgress = 0.5f;
    private const float SegmentProgressScale = 2f;
    private const float ControlDistanceRatio = 0.5f;
    private const float PrepareRetreatPeakProgress = 0.25f;

    private readonly FlyingMeleeAttackPattern _pattern;
    private readonly Vector2 _start;
    private readonly Vector2 _aim;
    private readonly Vector2 _end;
    private readonly Vector2 _entryControl;
    private readonly Vector2 _exitControl;

    public FlyingMeleeAttackPath(FlyingMeleeAttackPattern pattern, Vector2 start, Vector2 aim, Vector2 end)
    {
        _pattern = pattern;
        _start = start;
        _aim = aim;
        _end = end;

        // 두 곡선이 조준점을 수평으로 통과하며 같은 접선으로 연결된다.
        float direction = end.x >= start.x ? 1f : -1f;
        float controlDistance = Mathf.Min(Mathf.Abs(aim.x - start.x), Mathf.Abs(end.x - aim.x)) * ControlDistanceRatio;
        Vector2 tangent = Vector2.right * direction * controlDistance;
        _entryControl = aim - tangent;
        _exitControl = aim + tangent;
    }

    public Vector2 CalculatePosition(float progress)
    {
        float clampedProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
        if (_pattern == FlyingMeleeAttackPattern.Horizontal)
        {
            return Vector2.Lerp(_start, _end, clampedProgress);
        }

        return clampedProgress <= HalfProgress
            ? CalculateQuadraticPosition(_start, _entryControl, _aim, clampedProgress * SegmentProgressScale)
            : CalculateQuadraticPosition(_aim, _exitControl, _end, (clampedProgress - HalfProgress) * SegmentProgressScale);
    }

    public Vector2 CalculatePrepareRetreatOffset(float prepareProgress, float distance)
    {
        float progress = Mathf.Clamp01(prepareProgress);
        // 양 끝과 후퇴 정점에서 속도가 연속되도록 기존 전진 경로에 부드러운 후퇴량만 더한다.
        float weight = progress <= PrepareRetreatPeakProgress
            ? Mathf.SmoothStep(0f, 1f, progress / PrepareRetreatPeakProgress)
            : Mathf.SmoothStep(0f, 1f, (1f - progress) / (1f - PrepareRetreatPeakProgress));
        return -CalculateDirection(0f) * distance * weight;
    }

    public Vector2 CalculateDirection(float progress)
    {
        if (_pattern == FlyingMeleeAttackPattern.Horizontal)
        {
            return (_end - _start).normalized;
        }

        float clampedProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
        return clampedProgress <= HalfProgress
            ? CalculateQuadraticDirection(_start, _entryControl, _aim, clampedProgress * SegmentProgressScale)
            : CalculateQuadraticDirection(_aim, _exitControl, _end, (clampedProgress - HalfProgress) * SegmentProgressScale);
    }

    private static Vector2 CalculateQuadraticPosition(Vector2 start, Vector2 control, Vector2 end, float progress)
    {
        float remaining = 1f - progress;
        return remaining * remaining * start + SegmentProgressScale * remaining * progress * control + progress * progress * end;
    }

    private static Vector2 CalculateQuadraticDirection(Vector2 start, Vector2 control, Vector2 end, float progress)
    {
        return ((1f - progress) * (control - start) + progress * (end - control)).normalized;
    }
}
