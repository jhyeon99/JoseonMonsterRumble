using UnityEngine;

/// <summary>
/// 짧은 그로기 (찍기를 패링당했을 때). 보스는 가만히 있고 플레이어가 자유롭게 공격하는 시간.
/// 그로기 시간 → groggyEnd 트리거 → 일어나는 애니메이션이 끝난 뒤 다음 패턴 탐색 (대기 상태로).
/// </summary>
public class BossGroggyState : BossState
{
    private float _timer;
    private bool _isRecovering;

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
        if (_timer > 0f)
        {
            return;
        }

        if (!_isRecovering)
        {
            // 그로기 끝 → 일어나는 애니메이션 시작. 끝날 때까지 다음 패턴을 고르지 않음
            _isRecovering = true;
            EndGroggyAnimation();
            _timer = _boss.GroggyEndDuration;

            if (_timer > 0f)
            {
                return;
            }
        }

        _boss.GoIdle();
    }

    public override void Exit()
    {
        // 페이즈 전환 등으로 그로기 도중 끊겼을 때도 그로기 애니메이션 정리
        if (!_isRecovering)
        {
            EndGroggyAnimation();
        }
    }

    private void EndGroggyAnimation()
    {
        _boss.ResetTrigger(_boss.GroggyTrigger);
        _boss.SetBool(_boss.GroggyBool, false);
        _boss.SetTrigger(_boss.GroggyEndTrigger);
    }
}
