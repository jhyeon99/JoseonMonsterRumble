using UnityEngine;

public class PlayerDeadState : PlayerBaseState
{
    public PlayerDeadState(Player player, StateMachine stateMachine)
        : base(player, stateMachine)
    {
    }

    public override void Enter()
    {
        base.Enter();
        _rigidbody2D.linearVelocity = new Vector2(0, _rigidbody2D.linearVelocity.y);
        _player.TriggerDie();
    }
}
