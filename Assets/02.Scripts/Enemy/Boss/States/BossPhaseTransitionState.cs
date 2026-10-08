using UnityEngine;

/// <summary>1페이즈 체력 소진 → 하울링 연출 + 체력바 다시 참 → 2페이즈</summary>
public class BossPhaseTransitionState : BossState
{
    private float _timer;

    public BossPhaseTransitionState(Boss boss)
        : base(boss)
    {
    }

    public override void Enter()
    {
        _timer = _boss.HowlDuration;

        _boss.Log("페이즈 전환 (하울링)");
        _boss.ClearMinions();
        _boss.SnapToGround();
        _boss.FacePlayer();
        _boss.SetTrigger(_boss.HowlTrigger);

        _boss.Health.BeginPhase2(_boss.HowlDuration);
        _boss.SetPhase(2);

        CameraShake.Shake(_boss.HowlShake);
        CameraShake.Zoom(_boss.HowlZoom, _boss.CenterPosition);
    }

    public override void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            _boss.GoIdle();
        }
    }
}
