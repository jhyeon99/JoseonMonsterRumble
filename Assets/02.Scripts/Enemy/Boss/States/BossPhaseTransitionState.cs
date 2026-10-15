using UnityEngine;

/// <summary>1페이즈 체력 소진 → 하울링 연출 + 체력바 다시 참 → 2페이즈</summary>
public class BossPhaseTransitionState : BossState
{
    private float _timer;
    private float _vfxTimer;
    private bool _vfxPlayed;

    public BossPhaseTransitionState(Boss boss)
        : base(boss)
    {
    }

    public override void Enter()
    {
        _timer = _boss.HowlDuration;
        _vfxTimer = _boss.HowlVfxDelay;
        _vfxPlayed = false;

        _boss.Log("페이즈 전환 (하울링)");
        _boss.ClearMinions();
        _boss.SnapToGround();
        _boss.FacePlayer();
        // 진행 중이던 애니메이션(패턴 / 그로기 등)을 바로 끊고 대기에서 하울링 시작
        _boss.ForceIdleAnimation();
        _boss.SetTrigger(_boss.HowlTrigger);

        // 이펙트를 먼저 재생. SetPhase(2)가 부르는 페이즈 변경 이벤트(연출 등)에서 예외가 나도 이펙트는 나오게 함
        if (_vfxTimer <= 0f)
        {
            PlayHowlVfx();
        }

        _boss.Health.BeginPhase2(_boss.HowlDuration);
        _boss.SetPhase(2);

        CameraShake.Shake(_boss.HowlShake);
        CameraShake.Zoom(_boss.HowlZoom, _boss.CenterPosition);
    }

    public override void Update()
    {
        if (!_vfxPlayed)
        {
            _vfxTimer -= Time.deltaTime;
            if (_vfxTimer <= 0f)
            {
                PlayHowlVfx();
            }
        }

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            // 2페이즈 첫 패턴(소환 등)을 예약한 뒤 대기로 → 대기에서 바로 실행
            _boss.NotifyPhaseTransitionFinished();
            _boss.GoIdle();
        }
    }

    private void PlayHowlVfx()
    {
        _vfxPlayed = true;
        _boss.PlayHowlVfx();
    }
}
