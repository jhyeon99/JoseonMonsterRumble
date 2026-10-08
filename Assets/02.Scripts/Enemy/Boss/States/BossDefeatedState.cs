using UnityEngine;

/// <summary>마무리 공격을 맞고 처치 연출 → 일정 시간 뒤 비활성화</summary>
public class BossDefeatedState : BossState
{
    private float _timer;

    public BossDefeatedState(Boss boss)
        : base(boss)
    {
    }

    public override void Enter()
    {
        _timer = _boss.DefeatDuration;

        _boss.Log("처치");
        _boss.SetTrigger(_boss.DefeatTrigger);
        _boss.Health.HideBar();
        _boss.NotifyDefeated();
    }

    public override void Update()
    {
        if (_timer < 0f)
        {
            return;
        }

        _timer -= Time.deltaTime;
        if (_timer < 0f && _boss.DisableOnDefeat)
        {
            _boss.gameObject.SetActive(false);
        }
    }
}
