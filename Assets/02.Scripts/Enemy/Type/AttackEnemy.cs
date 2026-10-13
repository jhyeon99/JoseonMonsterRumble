using UnityEngine;

public class AttackEnemy : Enemy
{
    protected override void Start()
    {
        base.Start();
        StateMachine.ChangeState(new IdleState(this));
    }
}
