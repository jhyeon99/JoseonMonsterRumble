using UnityEngine;

/// <summary>
/// 짧은 그로기 (찍기를 패링당했을 때). 보스는 가만히 있고 플레이어가 자유롭게 공격하는 시간.
/// </summary>
public class BossGroggyState : BossState
{
    private float _timer;

    public BossGroggyState(Boss boss, float duration)
        : base(boss)
    {
        _timer = duration;
    }

    public override bool CanTakeDamage => true;

    public override void Enter()
    {
        _boss.Log($"그로기 {_timer}초");
        _boss.SnapToGround();

        // 이전 그로기에서 남은 종료 트리거가 바로 소비되지 않도록 정리
        _boss.ResetTrigger(_boss.GroggyEndTrigger);
        _boss.SetTrigger(_boss.GroggyTrigger);
        _boss.SetBool(_boss.GroggyBool, true);
    }

    public override void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            _boss.GoIdle();
        }
    }

    public override void Exit()
    {
        _boss.ResetTrigger(_boss.GroggyTrigger);
        _boss.SetBool(_boss.GroggyBool, false);
        _boss.SetTrigger(_boss.GroggyEndTrigger);
    }
}
