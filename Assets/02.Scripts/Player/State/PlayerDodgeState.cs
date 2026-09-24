using UnityEngine;

public class PlayerDodgeState : PlayerBaseState
{
    private float _elapsedTime;
    private float _backstepDirection;
    private bool _isDodgeMovementFinished;

    public PlayerDodgeState(Player player, StateMachine stateMachine) 
        : base(player, stateMachine) { }

    public override void Enter()
    {
        base.Enter();

        _elapsedTime = 0f;
        _isDodgeMovementFinished = false;

        _player.Health.SetInvincible(true);

        float facingDirection = _player.transform.localScale.x > 0 ? 1f : -1f;
        _backstepDirection = -facingDirection;

        _rigidbody2D.linearVelocity = new Vector2(_rigidbody2D.linearVelocity.x, _player.Movement.DodgeJumpForce);
    }

    public override void Execute()
    {
        base.Execute();

        if (_player.Health != null && _player.Health.IsDead)
        {
            _stateMachine.ChangeState(_player.DeadState);
            return;
        }

        if (!_isDodgeMovementFinished)
        {
            _elapsedTime += Time.deltaTime;
            if (_elapsedTime >= _player.Movement.DodgeDuration)
            {
                _isDodgeMovementFinished = true;
            }
        }
        else if (_player.IsGrounded)
        {
            if (Mathf.Abs(_inputHandler.MoveInputX) > 0.01f)
            {
                _stateMachine.ChangeState(_player.MoveState);
            }
            else
            {
                _stateMachine.ChangeState(_player.IdleState);
            }
        }
    }

    public override void PhysicsExecute()
    {
        base.PhysicsExecute();

        if (!_isDodgeMovementFinished)
        {
            float normalizedTime = Mathf.Clamp01(_elapsedTime / _player.Movement.DodgeDuration);
            float currentSpeedFactor = _player.Movement.DodgeSpeedCurve.Evaluate(normalizedTime);
            float currentDodgeSpeed = _backstepDirection * _player.Movement.DodgeDistance * currentSpeedFactor;

            _rigidbody2D.linearVelocity = new Vector2(currentDodgeSpeed, _rigidbody2D.linearVelocity.y);
        }
    }

    public override void Exit()
    {
        base.Exit();

        _player.Health.SetInvincible(false);
        _player.Movement.StartDodgeCooldown();
    }
}