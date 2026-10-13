using UnityEngine;

/// <summary>
/// 물약 복용 시 이동과 공격을 제한하고 제자리에서 치유 모션을 수행하는 상태.
/// 애니메이션 진행도가 70%에 도달하면 쿨타임 해제 및 연속 복용(체이닝)과 회피 캔슬을 허용한다.
/// </summary>
public class PlayerPotionState : PlayerBaseState
{
    private const string AnimStatePotion = "Player_Heal";
    private static readonly int AnimHashPotion = Animator.StringToHash(AnimStatePotion);
    private static readonly int AnimHashPotionFull = Animator.StringToHash("Base Layer." + AnimStatePotion);

    private const float ChainThresholdRatio = 0.7f;
    private const float PotionClipDuration = 0.6875f;
    private const float PotionStateSafetyTimeout = 0.85f;

    private float _elapsedTime;
    private bool _hasEnteredPotionAnimation;
    private RigidbodyConstraints2D _originalConstraints;

    public PlayerPotionState(Player player, StateMachine stateMachine)
        : base(player, stateMachine)
    {
    }

    public override void Enter()
    {
        base.Enter();

        _elapsedTime = 0f;
        _hasEnteredPotionAnimation = false;

        _originalConstraints = _rigidbody2D.constraints;
        _rigidbody2D.constraints = _originalConstraints | RigidbodyConstraints2D.FreezePositionX;
        _rigidbody2D.linearVelocity = new Vector2(0f, _rigidbody2D.linearVelocity.y);

        _player.SetRunning(false);
        _player.TriggerPotion();

        _player.Health.TryUsePotion();
    }

    public override void Execute()
    {
        base.Execute();

        _elapsedTime += Time.deltaTime;

        Animator animator = _player.Animator;
        float progress = 0f;
        bool isPlayingPotion = false;
        bool isAnimationFinished = false;

        if (animator != null)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.shortNameHash == AnimHashPotion)
            {
                _hasEnteredPotionAnimation = true;
                isPlayingPotion = true;
                progress = stateInfo.normalizedTime;

                if (progress >= 1.0f)
                {
                    isAnimationFinished = true;
                }
            }
            else if (_hasEnteredPotionAnimation && _elapsedTime >= PotionClipDuration * ChainThresholdRatio)
            {
                isAnimationFinished = true;
            }
        }

        if (!isPlayingPotion)
        {
            progress = Mathf.Clamp01(_elapsedTime / PotionClipDuration);
            if (_elapsedTime >= PotionClipDuration)
            {
                isAnimationFinished = true;
            }
        }

        // 70% 구간 도달 시 체이닝 및 캔슬 허용
        if (progress >= ChainThresholdRatio)
        {
            // 연속 복용 (에스트 체이닝): 쿨타임이 해제되었고 물약 사용 가능 시 즉시 재복용
            if (_player.Health.CanUsePotion && _inputHandler.TryUsePotionInput())
            {
                _elapsedTime = 0f;
                _hasEnteredPotionAnimation = false;
                _player.TriggerPotion();
                _player.Health.TryUsePotion();
                return;
            }

            // 회피 캔슬
            if (_player.IsGrounded && _player.Movement.CanDodge && _inputHandler.TryUseDodgeInput())
            {
                _stateMachine.ChangeState(_player.DodgeState);
                return;
            }
        }

        // 애니메이션 완료 또는 안전 타임아웃 도달 시 복귀 (단, 최소 70% 이상 진행된 경우에만 종료 허용)
        if (_elapsedTime >= PotionClipDuration * ChainThresholdRatio && (isAnimationFinished || _elapsedTime >= PotionStateSafetyTimeout))
        {
            ReturnToMovement();
        }
    }

    public override void PhysicsExecute()
    {
        base.PhysicsExecute();

        _rigidbody2D.linearVelocity = new Vector2(0f, _rigidbody2D.linearVelocity.y);
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
            _player.PlayIdleAnimation();
            _stateMachine.ChangeState(_player.IdleState);
        }
    }
}
