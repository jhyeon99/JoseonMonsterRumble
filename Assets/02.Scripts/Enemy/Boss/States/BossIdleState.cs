using UnityEngine;

/// <summary>패턴 사이 대기. 시간이 지나면 쓸 수 있는 패턴을 골라 실행</summary>
public class BossIdleState : BossState
{
    private const float RetryDelay = 0.3f;

    private float _timer;

    public BossIdleState(Boss boss, float waitTime)
        : base(boss)
    {
        _timer = waitTime;
    }

    public override bool CanTakeDamage => true;
    public override bool CanBeInterrupted => true;

    public override void Enter()
    {
        // 돌아보기 전 방향 기억 (꼬리치기 등 등 뒤 판정용)
        _boss.IdleEnterFacingX = _boss.FacingX;

        // 바로 써야 하는 패턴(등 뒤 꼬리치기 등)이 있으면 돌아보지 않고 Update에서 바로 사용
        if (_boss.HasForcedPattern)
        {
            return;
        }

        _boss.FacePlayer();
    }

    public override void Update()
    {
        // 바로 써야 하는 패턴은 돌아보기 전에 먼저 사용 (꼬리치기가 등을 보인 채 시작하도록)
        if (_boss.HasForcedPattern && _boss.TrySelectPattern(out BossPattern forced))
        {
            _boss.ChangeState(new BossPatternState(_boss, forced));
            return;
        }

        _boss.FacePlayer();

        _timer -= Time.deltaTime;

        // 체력 비율 발동 패턴은 대기 시간 없이 바로
        if (_timer > 0f && !_boss.HasForcedPattern)
        {
            return;
        }

        if (_boss.TrySelectPattern(out BossPattern pattern))
        {
            _boss.ChangeState(new BossPatternState(_boss, pattern));
            return;
        }

        // 지금 쓸 수 있는 패턴이 없으면 잠시 뒤 다시 확인
        _timer = RetryDelay;
    }
}
