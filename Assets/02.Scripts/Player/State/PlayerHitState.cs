using UnityEngine;

public class PlayerHitState : PlayerBaseState
{
    private RigidbodyConstraints2D _originalConstraints;
    private bool _hasEnteredHitAnimation;
    private float _elapsedTime;
    private const float SafetyTimeout = 2.5f;

    public PlayerHitState(Player player, StateMachine stateMachine)
        : base(player, stateMachine)
    {
    }

    public override void Enter()
    {
        base.Enter();

        _elapsedTime = 0f;
        _hasEnteredHitAnimation = false;

        _originalConstraints = _rigidbody2D.constraints;
        _rigidbody2D.constraints = _originalConstraints | RigidbodyConstraints2D.FreezePositionX;
        _rigidbody2D.linearVelocity = new Vector2(0f, _rigidbody2D.linearVelocity.y);

        _player.SetRunning(false);
        _player.TriggerHit();
    }

    public override void Execute()
    {
        base.Execute();

        _elapsedTime += Time.deltaTime;

        if (_elapsedTime >= _player.HitMotion.TotalDuration)
        {
            ReturnToMovement();
            return;
        }

        Animator animator = _player.Animator;
        if (animator != null)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

            if (stateInfo.IsName("Player_Hit"))
            {
                _hasEnteredHitAnimation = true;
            }

            if (_hasEnteredHitAnimation && (!stateInfo.IsName("Player_Hit") || stateInfo.normalizedTime >= 1.0f))
            {
                ReturnToMovement();
                return;
            }
        }
    }

    public override void PhysicsExecute()
    {
        base.PhysicsExecute();

        _player.Movement.MoveX(0f);
    }

    public override void Exit()
    {
        base.Exit();

        _rigidbody2D.constraints = _originalConstraints;
    }

    private void ReturnToMovement()
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
