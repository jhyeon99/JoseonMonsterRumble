using UnityEngine;
using Core.Interfaces;
using Core.FSM;

public class PlayerClimbState : IState
{
    private readonly Player _player;
    private readonly StateMachine _stateMachine;
    private readonly PlayerMovement _movement;
    private readonly PlayerInputHandler _input;

    private float _previousGravityScale;

    public PlayerClimbState(
        Player player,
        StateMachine stateMachine,
        PlayerMovement movement,
        PlayerInputHandler input)
    {
        _player = player;
        _stateMachine = stateMachine;
        _movement = movement;
        _input = input;
    }

    public void Enter()
    {
        if (_player.CurrentRope == null)
        {
            _stateMachine.ChangeState(_player.JumpState);
            return;
        }

        _previousGravityScale = _movement.GravityScale;

        _movement.SetGravityScale(0f);
        _movement.StopVelocity();
    }

    public void Update()
    {
        if (_player.CurrentRope == null)
        {
            ExitRope();
            return;
        }

        if (_input.ClimbExitPressed)
        {
            ExitRope();
        }
    }

    public void PhysicsUpdate()
    {
        ClimbableRope rope = _player.CurrentRope;

        if (rope == null)
        {
            return;
        }

        CheckRopeEnd(rope);

        if (_player.CurrentRope == null)
        {
            return;
        }

        _movement.Climb(
            rope.CenterX,
            _input.VerticalInput);
    }

    public void Exit()
    {
        _movement.SetGravityScale(_previousGravityScale);
    }

    private void CheckRopeEnd(ClimbableRope rope)
    {
        Vector2 position = _movement.Position;

        if (_input.VerticalInput > 0f &&
            position.y >= rope.TopY)
        {
            ReachTop(rope);
            return;
        }

        if (_input.VerticalInput < 0f &&
            position.y <= rope.BottomY)
        {
            ExitRope();
        }
    }

    private void ReachTop(ClimbableRope rope)
    {
        Vector2 exitPosition = rope.TopExitPosition;

        _movement.SetPosition(exitPosition);
        _movement.StopVelocity();

        _stateMachine.ChangeState(_player.IdleState);
    }

    private void ExitRope()
    {
        _stateMachine.ChangeState(_player.JumpState);
    }
}