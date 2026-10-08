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
        _boss.FacePlayer();
    }

    public override void Update()
    {
        _boss.FacePlayer();

        _timer -= Time.deltaTime;
        if (_timer > 0f)
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
