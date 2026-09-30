using UnityEngine;

public class PlayerClimbState : PlayerBaseState
{
    private ClimbableRope _climbingRope;

    public override PlatformInteractionMode PlatformMode => PlatformInteractionMode.FreeTrajectory;

    public PlayerClimbState(Player player, StateMachine stateMachine)
        : base(player, stateMachine)
    {
    }

    public override void Enter()
    {
        _climbingRope = _player.CurrentRope;
        if (_climbingRope == null)
        {
            LeaveRope();
            return;
        }

        _climbingRope.SetTopPlatformCollision(_player, true);
        _player.Movement.StartClimb();
    }

    public override void Execute()
    {
        if (_player.CurrentRope == null)
        {
            LeaveRope();
            return;
        }

        if (_inputHandler.TryUseJumpInput())
        {
            JumpOffRope();
            return;
        }

        if (Mathf.Abs(_inputHandler.MoveInputX) > 0.01f)
        {
            LeaveRope();
        }
    }

    public override void PhysicsExecute()
    {
        ClimbableRope rope = _player.CurrentRope;

        if (rope == null)
        {
            return;
        }

        CheckRopeEnd(rope);

        if (_stateMachine.CurrentState != this || _player.CurrentRope == null)
        {
            return;
        }

        _player.Movement.Climb(
            rope.CenterX,
            _inputHandler.MoveInputY);
    }

    public override void Exit()
    {
        _player.Movement.StopClimb();
        if (_climbingRope != null)
        {
            _climbingRope.SetTopPlatformCollision(_player, false);
            _climbingRope = null;
        }
    }

    private void CheckRopeEnd(ClimbableRope rope)
    {
        Vector2 feetPosition = _player.Movement.FeetPosition;

        if (_inputHandler.MoveInputY > 0f &&
            feetPosition.y >= rope.TopY)
        {
            ReachTop(rope);
            return;
        }

        if (_inputHandler.MoveInputY <= 0f &&
            feetPosition.y <= rope.BottomY)
        {
            LeaveRope();
        }
    }

    private void ReachTop(ClimbableRope rope)
    {
        Vector2 exitPosition = rope.TopExitPosition;

        _player.Movement.SetFeetPosition(exitPosition);
        _player.BlockRopeRegrabUntilVerticalRelease();
        _player.ExitRope(rope);

        _stateMachine.ChangeState(_player.IdleState);
    }

    private void LeaveRope()
    {
        _player.BlockRopeRegrabUntilVerticalRelease();
        _player.ExitRope(_player.CurrentRope);

        if (Mathf.Abs(_inputHandler.MoveInputX) > 0.01f)
        {
            _stateMachine.ChangeState(_player.MoveState);
            return;
        }

        _stateMachine.ChangeState(_player.IdleState);
    }

    private void JumpOffRope()
    {
        _player.BlockRopeRegrabUntilVerticalRelease();
        _player.ExitRope(_player.CurrentRope);
        _stateMachine.ChangeState(_player.JumpState);
    }
}
