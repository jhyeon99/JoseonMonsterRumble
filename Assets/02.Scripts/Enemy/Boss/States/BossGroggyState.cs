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
        _boss.SetBool(_boss.GroggyBool, false);
    }
}
