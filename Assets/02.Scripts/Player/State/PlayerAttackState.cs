using UnityEngine;

public class PlayerAttackState : PlayerBaseState
{
    private float _elapsedTime;
    private RigidbodyConstraints2D _originalConstraints;
    private bool _hasEnteredAttackAnimation;

    private bool _hasForwardMotion;
    private float _forwardDelay;
    private float _forwardSpeed;
    private float _forwardDuration;
    private float _forwardDirection;
    private bool _isForwarding;
    private bool _forwardCompleted;

    public PlayerAttackState(Player player, StateMachine stateMachine)
        : base(player, stateMachine)
    {
    }

    public override void Enter()
    {
        base.Enter();

        _elapsedTime = 0f;
        _hasEnteredAttackAnimation = false;

        _originalConstraints = _rigidbody2D.constraints;
        _rigidbody2D.constraints = _originalConstraints | RigidbodyConstraints2D.FreezePositionX;
        _rigidbody2D.linearVelocity = new Vector2(0f, _rigidbody2D.linearVelocity.y);

        if (_player.Attack.TryGetAttackData(0, out AttackData attackData) &&
            attackData.ForwardSpeed > 0f && attackData.ForwardDuration > 0f)
        {
            _hasForwardMotion = true;
            _forwardDelay = attackData.ForwardDelay;
            _forwardSpeed = attackData.ForwardSpeed;
            _forwardDuration = attackData.ForwardDuration;
            _forwardDirection = _player.transform.localScale.x > 0f ? 1f : -1f;
            _isForwarding = false;
            _forwardCompleted = false;
        }
        else
        {
            _hasForwardMotion = false;
            _forwardCompleted = true;
        }

        _player.SetRunning(false);
        _player.TriggerAttack();
        _player.Attack.ExecuteAttack();
    }

    public override void PhysicsExecute()
    {
        base.PhysicsExecute();

        if (_hasForwardMotion && !_forwardCompleted)
        {
            if (_elapsedTime >= _forwardDelay && _elapsedTime < _forwardDelay + _forwardDuration)
            {
                if (!_isForwarding)
                {
                    _isForwarding = true;
                    _rigidbody2D.constraints = _originalConstraints;
                }

                _rigidbody2D.linearVelocity = new Vector2(_forwardDirection * _forwardSpeed, _rigidbody2D.linearVelocity.y);
            }
            else if (_isForwarding || _elapsedTime >= _forwardDelay + _forwardDuration)
            {
                StopForwardMovement();
            }
        }
    }

    private void StopForwardMovement()
    {
        if (_forwardCompleted) return;

        _isForwarding = false;
        _forwardCompleted = true;
        _rigidbody2D.constraints = _originalConstraints | RigidbodyConstraints2D.FreezePositionX;
        _rigidbody2D.linearVelocity = new Vector2(0f, _rigidbody2D.linearVelocity.y);
    }

    public override void Execute()
    {
        base.Execute();

        _elapsedTime += Time.deltaTime;

        const float safetyTimeout = 5.0f;
        if (_elapsedTime >= safetyTimeout)
        {
            EndAttack();
            return;
        }

        Animator animator = _player.Animator;
        if (animator != null)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            bool inTransition = animator.IsInTransition(0);
            AnimatorStateInfo nextInfo = inTransition ? animator.GetNextAnimatorStateInfo(0) : default;

            bool isCurrentAttackOrRecover = IsAttackOrRecoverState(stateInfo);
            bool isNextAttackOrRecover = inTransition && IsAttackOrRecoverState(nextInfo);

            if (isCurrentAttackOrRecover || isNextAttackOrRecover)
            {
                _hasEnteredAttackAnimation = true;
            }

            bool isRecovering = stateInfo.IsTag("AttackRecover");
            bool isTransitioningToRecover = inTransition && nextInfo.IsTag("AttackRecover");

            if (isRecovering || isTransitioningToRecover)
            {
                if (_isForwarding || !_forwardCompleted)
                {
                    StopForwardMovement();
                }

                if (isRecovering && stateInfo.normalizedTime >= 0.5f)
                {
                    if (_inputHandler.TryUseDodgeInput() && _player.IsGrounded && _player.Movement.CanDodge)
                    {
                        _stateMachine.ChangeState(_player.DodgeState);
                        return;
                    }
                }

                if (isRecovering && !inTransition && stateInfo.normalizedTime >= 1.0f)
                {
                    EndAttack();
                    return;
                }
            }
            else if (_hasEnteredAttackAnimation && !isCurrentAttackOrRecover && !isNextAttackOrRecover)
            {
                EndAttack();
                return;
            }
        }
    }

    private bool IsAttackOrRecoverState(AnimatorStateInfo stateInfo)
    {
        if (stateInfo.IsTag("AttackRecover"))
        {
            return true;
        }

        return stateInfo.IsName("Player_Attack") ||
               stateInfo.IsName("Player_Attack_1st") ||
               stateInfo.IsName("Player_New_Attack_1st") ||
               stateInfo.IsName("Player_Attack_2nd") ||
               stateInfo.IsName("Player_Attack_3rd") ||
               stateInfo.IsName("Player_Attack_2nd_Ready") ||
               stateInfo.IsName("Player_Attack_3rd_Ready");
    }

    private void EndAttack()
    {
        _stateMachine.ChangeState(_player.IdleState);
    }

    public override void Exit()
    {
        base.Exit();

        _rigidbody2D.constraints = _originalConstraints;
        _player.Attack.StopAttack();
    }
}