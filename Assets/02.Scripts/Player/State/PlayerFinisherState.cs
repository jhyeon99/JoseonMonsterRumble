using UnityEngine;

/// <summary>
/// 그로기 대상(IFinishable) 마무리 공격. 3타 모션을 바로 재생하고 타격 순간에 대상에게 마무리를 전달.
/// 일반 적은 날려 보내고, 보스는 처치 연출로 넘어감.
/// 마무리 공격 중에는 무적.
/// </summary>
public class PlayerFinisherState : PlayerBaseState
{
    private const string AttackAnimationState = "Player_Attack_3rd";
    private const string ReadyAnimationState = "Player_Attack_3rd_Ready";
    private const string RecoverAnimationState = "Player_Attack_3rd_Recover";

    private IFinishable _target;
    private float _elapsedTime;
    private float _impactDelay;
    private float _duration;
    private bool _hasImpacted;
    private bool _hasEnteredFinisherAnimation;
    private bool _hasPlayedParryEffect;
    private float _parryEffectNormalizedTime;
    private float _impactNormalizedTime;
    private RigidbodyConstraints2D _originalConstraints;

    public PlayerFinisherState(Player player, StateMachine stateMachine)
        : base(player, stateMachine)
    {
    }

    public void SetTarget(IFinishable target)
    {
        _target = target;
    }

    public override void Enter()
    {
        base.Enter();

        _elapsedTime = 0f;
        _hasImpacted = false;
        _hasEnteredFinisherAnimation = false;
        _hasPlayedParryEffect = false;
        _parryEffectNormalizedTime = _player.Finisher.GetParryEffectNormalizedTime(_player.Animator);
        _impactNormalizedTime = _player.Finisher.GetImpactNormalizedTime(_player.Animator);
        _player.Finisher.GetTiming(_player.Animator, _player.Attack, out _impactDelay, out _duration);

        _originalConstraints = _rigidbody2D.constraints;
        _rigidbody2D.constraints = _originalConstraints | RigidbodyConstraints2D.FreezePositionX;
        _rigidbody2D.linearVelocity = new Vector2(0f, _rigidbody2D.linearVelocity.y);

        _player.SetRunning(false);
        FaceTarget();

        // 마무리 공격 중에는 무적 (반투명 표시 없이)
        _player.Health.SetFinisherInvincible(true);

        // 준비 동작 중에 그로기가 풀리지 않게 고정
        if (IsTargetAlive())
        {
            _target.TryHoldForFinisher();
        }

        _player.GetComponent<PlayerAudio>()?.BeginFinisher();
        _player.PlayFinisherAnimation();
    }

    public override void Execute()
    {
        base.Execute();

        _elapsedTime += Time.deltaTime;

        UpdateFinisherAnimation();

        if (!_hasImpacted && float.IsPositiveInfinity(_impactNormalizedTime) && _elapsedTime >= _impactDelay)
        {
            Impact();
        }

        if (HasFinishedFinisherAnimation())
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

        _player.Movement.MoveX(0f);
    }

    public override void Exit()
    {
        base.Exit();

        _rigidbody2D.constraints = _originalConstraints;
        _player.Health.SetFinisherInvincible(false);

        // 타격 전에 끊겼으면 그로기 고정 해제
        if (!_hasImpacted && IsTargetAlive())
        {
            _target.ReleaseFinisherHold();
        }

        if (!_hasImpacted)
        {
            GameEvents.TriggerFinisherAnticipationCancelled();
        }

        _target = null;
    }

    private void Impact()
    {
        _hasImpacted = true;

        if (!IsTargetAlive() || !_target.CanBeFinished)
        {
            GameEvents.TriggerFinisherAnticipationCancelled();
            return;
        }

        Transform targetTransform = _target.FinisherTransform;
        float directionX = targetTransform.position.x >= _player.transform.position.x ? 1f : -1f;
        Vector2 point = targetTransform.position;

        _target.ReceiveFinisher(_player.Finisher.Launch, directionX);
        GameEvents.TriggerFinisher(point, _player.transform, targetTransform);
    }

    private bool HasFinishedFinisherAnimation()
    {
        Animator animator = _player.Animator;
        if (animator == null) return _elapsedTime >= _duration;
        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        bool transitioning = animator.IsInTransition(0);
        bool isCurrentFinisher = IsFinisherAnimation(current);
        bool isNextFinisher = transitioning && IsFinisherAnimation(animator.GetNextAnimatorStateInfo(0));
        if (isCurrentFinisher || isNextFinisher)
        {
            _hasEnteredFinisherAnimation = true;
            return current.IsName(RecoverAnimationState) && current.normalizedTime >= 1f && !transitioning;
        }
        return _hasEnteredFinisherAnimation || _elapsedTime >= _duration;
    }

    private static bool IsFinisherAnimation(AnimatorStateInfo state)
    {
        return state.IsName(ReadyAnimationState) || state.IsName(AttackAnimationState) || state.IsName(RecoverAnimationState);
    }

    private void UpdateFinisherAnimation()
    {
        Animator animator = _player.Animator;
        if (animator == null)
        {
            return;
        }

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        if (!stateInfo.IsName(AttackAnimationState))
        {
            return;
        }

        if (!_hasPlayedParryEffect && stateInfo.normalizedTime >= _parryEffectNormalizedTime)
        {
            _hasPlayedParryEffect = true;
            _player.Finisher.PlayParryAttackEffect();
        }

        if (_hasImpacted || float.IsPositiveInfinity(_impactNormalizedTime))
        {
            return;
        }
        if (!IsTargetAlive())
        {
            GameEvents.TriggerFinisherAnticipationCancelled();
            return;
        }

        float progress = Mathf.Clamp01(stateInfo.normalizedTime / _impactNormalizedTime);
        GameEvents.TriggerFinisherAnticipation(_player.transform, _target.FinisherTransform, progress);
        if (stateInfo.normalizedTime >= _impactNormalizedTime)
        {
            Impact();
        }
    }

    // 대상이 파괴됐거나 꺼졌는지 확인.
    private bool IsTargetAlive()
    {
        return _target is Behaviour behaviour && behaviour != null && behaviour.isActiveAndEnabled;
    }

    private void FaceTarget()
    {
        if (!IsTargetAlive())
        {
            return;
        }

        float diff = _target.FinisherTransform.position.x - _player.transform.position.x;
        if (Mathf.Abs(diff) > 0.01f)
        {
            _player.transform.localScale = new Vector3(Mathf.Sign(diff), 1f, 1f);
        }
    }
}
