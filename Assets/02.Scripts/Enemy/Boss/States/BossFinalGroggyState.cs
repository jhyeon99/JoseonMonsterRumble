/// <summary>
/// 2페이즈 체력 소진 → 완전 그로기. 플레이어가 앞에서 공격하면 마무리 공격 (Boss.ReceiveFinisher).
/// 이 상태에서는 데미지를 받지 않음.
/// </summary>
public class BossFinalGroggyState : BossState
{
    public BossFinalGroggyState(Boss boss)
        : base(boss)
    {
    }

    public override void Enter()
    {
        _boss.Log("완전 그로기 (마무리 공격 대기)");
        _boss.ClearMinions();
        _boss.SnapToGround();
        _boss.ResetTrigger(_boss.GroggyEndTrigger);
        _boss.SetTrigger(_boss.GroggyTrigger);
        _boss.SetBool(_boss.GroggyBool, true);

        // 머리 위에 마무리 공격 대기 표시
        _boss.ShowFinishMarker();
    }

    public override void Exit()
    {
        _boss.SetBool(_boss.GroggyBool, false);
        _boss.HideFinishMarker();
    }
}
