using UnityEngine;

public sealed class PlayerAirAttackState : PlayerBaseState
{
    private const string AnimationStateName = "PlayerAirAttack";
    private const string AirAnimationStateName = "Player_JumpAir";
    private const string LandingAnimationStateName = "Player_JumpEnd";
    private const float SafetyTimeout = 5f;
    private const float MovementThreshold = 0.01f;
    private const float LandingVelocityThreshold = 0.1f;
    private const int AnimationLayer = 0;

    public static readonly int AnimationStateHash = Animator.StringToHash(AnimationStateName);
    public static readonly int FullAnimationStateHash = Animator.StringToHash("Base Layer." + AnimationStateName);
    private static readonly int AirAnimationStateHash = Animator.StringToHash(AirAnimationStateName);
    private static readonly int LandingAnimationStateHash = Animator.StringToHash(LandingAnimationStateName);

    private enum HitPhase { Waiting, Active, Finished }
    private HitPhase _hitPhase;
    private float _elapsedTime;
    private bool _hasEnteredAnimation;
    private float _hitStartRatio;
    private float _hitEndRatio;

    public override PlatformInteractionMode PlatformMode => PlatformInteractionMode.FreeTrajectory;

    public PlayerAirAttackState(Player player, StateMachine stateMachine) : base(player, stateMachine)
    {
    }

    public override void Enter()
    {
        _hitPhase = HitPhase.Waiting;
        _elapsedTime = 0f;
        _hasEnteredAnimation = false;
        _player.SetRunning(false);
        _player.Movement.SetMovingPlatform(null);
        _player.Attack.TryGetAirAttackData(out AttackData attackData);
        _hitStartRatio = attackData.HitStartRatio;
        _hitEndRatio = Mathf.Max(_hitStartRatio, attackData.HitEndRatio);
        _player.Attack.StartAirAttackStep();
        _player.Animator.Play(AnimationStateHash, AnimationLayer, 0f);
    }

    public override void Execute()
    {
        _elapsedTime += Time.deltaTime;
        float platformVelocityY = _player.Movement.CurrentPlatform != null
            ? _player.Movement.CurrentPlatform.Velocity.y : 0f;
        if (_player.IsGrounded && _rigidbody2D.linearVelocity.y - platformVelocityY <= LandingVelocityThreshold)
        {
            _player.Movement.OnLanded();
            ReturnToMovement();
            return;
        }

        AnimatorStateInfo animation = _player.Animator.GetCurrentAnimatorStateInfo(AnimationLayer);
        if (animation.shortNameHash == AnimationStateHash)
        {
            _hasEnteredAnimation = true;
            float progress = Mathf.Clamp01(animation.normalizedTime);
            if (_hitPhase == HitPhase.Waiting && progress >= _hitStartRatio)
            {
                _hitPhase = HitPhase.Active;
                _player.Attack.BeginAirAttackHitbox();
            }
            if (_hitPhase == HitPhase.Active && progress >= _hitEndRatio)
            {
                _hitPhase = HitPhase.Finished;
                _player.Attack.EndHitbox();
            }
            if (progress < 1f && _elapsedTime < SafetyTimeout)
            {
                return;
            }
        }
        else if (!_hasEnteredAnimation && _elapsedTime < SafetyTimeout)
        {
            return;
        }

        _player.Attack.CompleteAttackStep();
        ReturnToMovement();
    }

    public override void PhysicsExecute()
    {
        _player.Movement.MoveX(_inputHandler.MoveInputX);
    }

    public override void Exit()
    {
        _player.Attack.StopAttack();
        _inputHandler.ResetAttackInput();
        _hitPhase = HitPhase.Waiting;
        _elapsedTime = 0f;
        _hasEnteredAnimation = false;
        Animator animator = _player.Animator;
        _hitStartRatio = 0f;
        _hitEndRatio = 0f;
        if (animator.GetCurrentAnimatorStateInfo(AnimationLayer).shortNameHash == AnimationStateHash)
        {
            animator.Play(_player.IsGrounded ? LandingAnimationStateHash : AirAnimationStateHash, AnimationLayer, 0f);
        }
    }

    private void ReturnToMovement()
    {
        _stateMachine.ChangeState(Mathf.Abs(_inputHandler.MoveInputX) > MovementThreshold
            ? _player.MoveState : _player.IdleState);
    }
}
