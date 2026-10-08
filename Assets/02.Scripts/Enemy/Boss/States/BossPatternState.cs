/// <summary>선택된 패턴 실행. 패턴이 끝나면 대기로, 중간에 끊기면 패턴 정리</summary>
public class BossPatternState : BossState
{
    private readonly BossPattern _pattern;

    public BossPatternState(Boss boss, BossPattern pattern)
        : base(boss)
    {
        _pattern = pattern;
    }

    public BossPattern Pattern => _pattern;

    public override bool CanTakeDamage => true;
    public override bool CanBeInterrupted => true;

    public override void Enter()
    {
        _boss.Log($"패턴 시작: {_pattern.PatternName}");
        _pattern.Begin(HandlePatternFinished);
    }

    public override void Exit()
    {
        // 패링 그로기 / 페이즈 전환으로 끊겼으면 판정, 예고, 위치 정리
        _pattern.Stop();
    }

    private void HandlePatternFinished()
    {
        if (_boss.CurrentState == this)
        {
            _boss.GoIdle();
        }
    }
}
