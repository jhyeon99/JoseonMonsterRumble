using UnityEngine;

public class AttackEnemy : Enemy
{
    protected override void Start()
    {
        base.Start();
        StateMachine.ChangeState(new IdleState(this));
    }

    protected override void Update()
    {
        EnemyState state = StateMachine.CurrentState;
        if (MapTestMode.IsEnabled && state != null &&
            !(state is IdleState) && !(state is ReturnToStartState) &&
            !(state is KnockbackState) && !IsDefeated && !IsGroggy)
        {
            // 공격 상태의 Exit로 돌진/판정 수명을 정리하고 기존 순찰로 돌아간다.
            StateMachine.ChangeState(new IdleState(this));
            Combat?.StopAttack();
            Animation.ResetAttackTrigger();
        }

        base.Update();
    }
}
