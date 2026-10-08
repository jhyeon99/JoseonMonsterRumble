using UnityEngine;

/// <summary>등장 연출. 끝나면 대기 → 패턴 시작</summary>
public class BossIntroState : BossState
{
    private float _timer;

    public BossIntroState(Boss boss)
        : base(boss)
    {
    }

    public override void Enter()
    {
        _timer = _boss.IntroDuration;
        _boss.FacePlayer();
        _boss.SetTrigger(_boss.IntroTrigger);
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
