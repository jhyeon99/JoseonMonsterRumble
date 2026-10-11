using UnityEngine;

/// <summary>등장 연출 (하울링). 끝나면 대기 → 패턴 시작</summary>
public class BossIntroState : BossState
{
    private float _timer;
    private float _vfxTimer;
    private bool _vfxPlayed;

    public BossIntroState(Boss boss)
        : base(boss)
    {
    }

    public override void Enter()
    {
        _timer = _boss.IntroDuration;
        _boss.FacePlayer();
        _boss.SetTrigger(_boss.IntroTrigger);

        // 등장도 하울링이므로 페이즈 전환과 같은 입 이펙트 재생
        _vfxTimer = _boss.HowlVfxDelay;
        _vfxPlayed = !_boss.PlayIntroHowlVfx;
        if (!_vfxPlayed && _vfxTimer <= 0f)
        {
            PlayHowlVfx();
        }
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
        // 등장 연출(BossCinematicDirector)이 끝날 때까지 대기
        if (_timer <= 0f && !_boss.HoldIntro)
        {
            _boss.GoIdle();
        }
    }

    private void PlayHowlVfx()
    {
        _vfxPlayed = true;
        _boss.PlayHowlVfx();
    }
}
