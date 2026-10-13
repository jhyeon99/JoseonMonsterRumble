using UnityEngine;

public class PlayerDodgeState : PlayerBaseState
{
    private float _elapsedTime;
    private float _dodgeDirection;
    private Vector2 _startPosition;
    private RigidbodyConstraints2D _originalConstraints;
    private bool _hasEnteredDodgeAnimation;
    private bool _isInvincibleEnded;
    private bool _isDashEnded;

    public override PlatformInteractionMode PlatformMode => PlatformInteractionMode.ManualWithDelta;

    public PlayerDodgeState(Player player, StateMachine stateMachine)
        : base(player, stateMachine)
    {
    }

    public override void Enter()
    {
        base.Enter();

        _elapsedTime = 0f;
        _hasEnteredDodgeAnimation = false;
        _isInvincibleEnded = false;
        _isDashEnded = false;

        _originalConstraints = _rigidbody2D.constraints;

        _player.Health.SetInvincible(true);
        _player.TriggerDodge();

        // 방향키 입력이 있으면 먼저 그 방향으로 뒤를 돌아봄
        float inputX = _inputHandler.MoveInputX;
        if (Mathf.Abs(inputX) > 0.01f)
        {
            _player.transform.localScale = new Vector3(Mathf.Sign(inputX), 1f, 1f);
        }

        // 닷지는 항상 현재 바라보고 있는 방향의 반대쪽(순수 백스텝)으로 이동
        float facingDirection = _player.transform.localScale.x > 0 ? 1f : -1f;
        _dodgeDirection = -facingDirection;

        _startPosition = _rigidbody2D.position;
        _rigidbody2D.linearVelocity = new Vector2(0f, _rigidbody2D.linearVelocity.y);
    }

    public override void Execute()
    {
        base.Execute();

        _elapsedTime += Time.deltaTime;

        if (!_isInvincibleEnded && _elapsedTime >= _player.Movement.DodgeInvincibleDuration)
        {
            _player.Health.SetInvincible(false);
            _isInvincibleEnded = true;
        }

        float totalDuration = _player.DodgeMotion.TotalDuration;
        if (totalDuration <= 0f)
        {
            totalDuration = 0.67f;
        }

        if (_elapsedTime >= totalDuration)
        {
            EndDodge();
            return;
        }

        Animator animator = _player.Animator;
        if (animator != null)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

            if (stateInfo.IsName("Player_dodge"))
            {
                _hasEnteredDodgeAnimation = true;
            }

            if (_hasEnteredDodgeAnimation && (!stateInfo.IsName("Player_dodge") || stateInfo.normalizedTime >= 1.0f))
            {
                EndDodge();
                return;
            }
        }
    }

    public override void PhysicsExecute()
    {
        base.PhysicsExecute();

        float dodgeDuration = _player.Movement.DodgeDuration;

        if (_elapsedTime < dodgeDuration)
        {
            float normalizedTime = Mathf.Clamp01(_elapsedTime / dodgeDuration);
            float progress = _player.Movement.DodgeSpeedCurve.Evaluate(normalizedTime);

            MovingPlatform platform = _player.Movement.CurrentPlatform;
            if (platform != null)
            {
                _startPosition += platform.DeltaPosition;
            }

            float targetX = _startPosition.x + _dodgeDirection * _player.Movement.DodgeDistance * progress;
            _rigidbody2D.MovePosition(new Vector2(targetX, _startPosition.y));
        }
        else
        {
            if (!_isDashEnded)
            {
                _isDashEnded = true;
                _rigidbody2D.linearVelocity = new Vector2(0f, _rigidbody2D.linearVelocity.y);
                _rigidbody2D.constraints = _originalConstraints | RigidbodyConstraints2D.FreezePositionX;
            }
        }
    }

    private void EndDodge()
    {
        if (Mathf.Abs(_inputHandler.MoveInputX) > 0.01f)
            _stateMachine.ChangeState(_player.MoveState);
        else
            _stateMachine.ChangeState(_player.IdleState);
    }

    public override void Exit()
    {
        base.Exit();

        _rigidbody2D.constraints = _originalConstraints;
        _player.Health.SetInvincible(false);
        _player.Movement.StartDodgeCooldown();
    }
}