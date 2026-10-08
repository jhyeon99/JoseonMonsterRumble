using UnityEngine;

/// <summary>
/// 아래 방향키를 누르고 있는 동안 가드. 누르기 시작한 직후가 패링 판정 (PlayerGuard).
/// 키를 떼거나 가드 게이지가 바닥나면 해제.
/// </summary>
public class PlayerGuardState : PlayerBaseState
{
    private enum GuardPhase
    {
        Guarding,
        WaitingForRecoveryAnimation,
        Recovering,
    }

    private const string AnimStateGuardEnd = "player_guard_end";
    private const float GuardRecoverySafetyTimeout = 1f;

    private GuardPhase _phase;
    private float _blockKnockbackVelocityX;
    private float _guardRecoveryElapsedTime;

    public PlayerGuardState(Player player, StateMachine stateMachine)
        : base(player, stateMachine)
    {
    }

    public override void Enter()
    {
        base.Enter();

        _blockKnockbackVelocityX = 0f;
        _player.SetRunning(false);
        _rigidbody2D.linearVelocity = new Vector2(0f, _rigidbody2D.linearVelocity.y);

        StartGuarding();
    }

    public override void Execute()
    {
        base.Execute();

        if (_phase != GuardPhase.Guarding)
        {
            HandleGuardRecovery();
            return;
        }

        if (!_inputHandler.IsGuardKey || _player.Guard.IsGaugeEmpty)
        {
            StartGuardRecovery();
            return;
        }

        // 패링 성공 직후 공격키 → 즉시 3타 공격으로 반격
        if (_inputHandler.TryUseAttackInput())
        {
            if (_player.Guard.CanParryCounter)
            {
                _player.Guard.ConsumeParryCounter();
                _player.AttackState.SetStartingComboIndex(2);
                _stateMachine.ChangeState(_player.AttackState);
                return;
            }

            if (_player.TryEnterFinisher())
            {
                return;
            }
        }

        if (_inputHandler.TryUseDodgeInput() && _player.Movement.CanDodge)
        {
            _stateMachine.ChangeState(_player.DodgeState);
        }
    }

    public override void PhysicsExecute()
    {
        base.PhysicsExecute();

        if (_phase != GuardPhase.Guarding)
        {
            _player.Movement.MoveX(0f);
            return;
        }

        if (!Mathf.Approximately(_blockKnockbackVelocityX, 0f))
        {
            Vector2 velocity = _rigidbody2D.linearVelocity;
            velocity.x = _blockKnockbackVelocityX;
            _rigidbody2D.linearVelocity = velocity;

            _blockKnockbackVelocityX = Mathf.MoveTowards(
                _blockKnockbackVelocityX,
                0f,
                _player.Guard.BlockKnockbackDeceleration * Time.fixedDeltaTime);
        }
        else
        {
            _player.Movement.MoveX(0f);
        }

        // 가드 중에는 이동하지 않고 좌우 키로 바라보는 방향만 바꿈
        float inputX = _inputHandler.MoveInputX;
        if (Mathf.Abs(inputX) > 0.01f)
        {
            _player.transform.localScale = new Vector3(Mathf.Sign(inputX), 1f, 1f);
        }
    }

    public override void Exit()
    {
        base.Exit();

        _phase = GuardPhase.Guarding;
        _blockKnockbackVelocityX = 0f;
        _guardRecoveryElapsedTime = 0f;
        _player.Guard.EndGuard();
        _player.SetGuarding(false);
    }

    public void StartBlockKnockback(Vector2 attackDirection)
    {
        float directionX = attackDirection.x >= 0f ? 1f : -1f;
        _blockKnockbackVelocityX = directionX * _player.Guard.BlockKnockbackSpeed;
    }

    private void StartGuarding()
    {
        _phase = GuardPhase.Guarding;
        _blockKnockbackVelocityX = 0f;
        _guardRecoveryElapsedTime = 0f;

        _player.SetGuarding(true);
        _player.TriggerGuardStart();

        // 방금 아래 방향키를 눌러서 들어온 경우만 패링 판정을 엶
        _player.Guard.BeginGuard(_inputHandler.TryUseGuardInput());
    }

    private void StartGuardRecovery()
    {
        _phase = GuardPhase.WaitingForRecoveryAnimation;
        _blockKnockbackVelocityX = 0f;
        _guardRecoveryElapsedTime = 0f;

        _rigidbody2D.linearVelocity = new Vector2(0f, _rigidbody2D.linearVelocity.y);
        _player.Guard.EndGuard();
        _player.SetGuarding(false);
    }

    private void HandleGuardRecovery()
    {
        if (_inputHandler.IsGuardKey && _player.IsGrounded && _player.Guard.CanStartGuard)
        {
            StartGuarding();
            return;
        }

        if (_inputHandler.TryUseAttackInput() && _player.IsGrounded)
        {
            if (_player.Guard.CanParryCounter)
            {
                _player.Guard.ConsumeParryCounter();
                _player.AttackState.SetStartingComboIndex(2);
                _stateMachine.ChangeState(_player.AttackState);
            }
            else if (!_player.TryEnterFinisher())
            {
                _stateMachine.ChangeState(_player.AttackState);
            }

            return;
        }

        if (_inputHandler.TryUseDodgeInput() && _player.IsGrounded && _player.Movement.CanDodge)
        {
            _stateMachine.ChangeState(_player.DodgeState);
            return;
        }

        UpdateGuardRecoveryAnimation();
    }

    private void UpdateGuardRecoveryAnimation()
    {
        _guardRecoveryElapsedTime += Time.deltaTime;

        Animator animator = _player.Animator;
        if (animator == null)
        {
            ReturnToMovement();
            return;
        }

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        bool isInTransition = animator.IsInTransition(0);
        AnimatorStateInfo nextStateInfo = isInTransition
            ? animator.GetNextAnimatorStateInfo(0)
            : default;

        bool isGuardEndState = stateInfo.IsName(AnimStateGuardEnd);
        bool isNextGuardEndState = isInTransition && nextStateInfo.IsName(AnimStateGuardEnd);

        if (_phase == GuardPhase.WaitingForRecoveryAnimation &&
            (isGuardEndState || isNextGuardEndState))
        {
            _phase = GuardPhase.Recovering;
        }

        if (_phase == GuardPhase.Recovering)
        {
            if (!isGuardEndState && !isNextGuardEndState)
            {
                ReturnToMovement();
                return;
            }

            if (isGuardEndState && !isInTransition && stateInfo.normalizedTime >= 1f)
            {
                ReturnToMovement();
                return;
            }
        }

        if (_guardRecoveryElapsedTime >= GuardRecoverySafetyTimeout)
        {
            Debug.LogWarning(
                $"{_player.name}: {AnimStateGuardEnd} 애니메이션 완료를 확인하지 못해 가드 종료 상태를 해제합니다.",
                _player);
            ReturnToMovement();
        }
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
