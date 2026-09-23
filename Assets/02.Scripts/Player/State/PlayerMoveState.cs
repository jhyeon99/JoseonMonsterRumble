using UnityEngine;

public class PlayerMoveState : PlayerBaseState
{
    public PlayerMoveState(Player player, StateMachine stateMachine) 
        : base(player, stateMachine) { }

    public override void Enter()
    {
        base.Enter();
    }

    public override void Execute()
    {
        base.Execute();
    }

    public override void PhysicsExecute()
    {
        base.PhysicsExecute();
    }

    public override void Exit()
    {
        base.Exit();
    }
}