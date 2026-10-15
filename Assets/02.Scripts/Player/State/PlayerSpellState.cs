using UnityEngine;

public class PlayerSpellState : PlayerBaseState
{
    private const float SpellStateSafetyTimeout = 3f;
    private const float SpellSuperArmorDuration = 0.15f;
    private const float SpellFireNormalizedTime = 0.35f;

    private static readonly int AnimHashSpell = Animator.StringToHash("PlayerSpell");
    private static readonly int AnimHashSpellEnd = Animator.StringToHash("PlayerSpellEnd");

    private float _elapsedTime;
    private float _superArmorElapsedTime;
    private bool _hasFired;
    private bool _hasEnteredSpellAnimation;
    private RigidbodyConstraints2D _originalConstraints;

    public bool IsSuperArmorActive => _hasFired && _superArmorElapsedTime < SpellSuperArmorDuration;

    public PlayerSpellState(Player player, StateMachine stateMachine)
        : base(player, stateMachine)
    {
    }

    public override void Enter()
    {
        base.Enter();

        _elapsedTime = 0f;
        _superArmorElapsedTime = 0f;
        _hasFired = false;
        _hasEnteredSpellAnimation = false;

        _originalConstraints = _rigidbody2D.constraints;
        _rigidbody2D.constraints = _originalConstraints | RigidbodyConstraints2D.FreezePositionX;
        _rigidbody2D.linearVelocity = new Vector2(0f, _rigidbody2D.linearVelocity.y);

        _player.SetRunning(false);
        _player.TriggerSpell();
    }

    public override void Execute()
    {
        base.Execute();

        _elapsedTime += Time.deltaTime;
        if (_hasFired)
        {
            _superArmorElapsedTime += Time.deltaTime;
        }

        Animator animator = _player.Animator;
        if (animator != null)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            bool isInTransition = animator.IsInTransition(0);
            AnimatorStateInfo nextStateInfo = isInTransition
                ? animator.GetNextAnimatorStateInfo(0)
                : default;

            bool isCurrentSpell = IsSpellState(stateInfo);
            bool isNextSpell = isInTransition && IsSpellState(nextStateInfo);
            bool isEnteringSpellEnd = isInTransition && nextStateInfo.shortNameHash == AnimHashSpellEnd;

            if (isCurrentSpell || isNextSpell)
            {
                _hasEnteredSpellAnimation = true;
            }

            bool isSpellCastPointReached = stateInfo.shortNameHash == AnimHashSpell && stateInfo.normalizedTime >= SpellFireNormalizedTime;

            if (!_hasFired &&
                (isSpellCastPointReached || stateInfo.shortNameHash == AnimHashSpellEnd || isEnteringSpellEnd))
            {
                FireSpell();
            }

            if (_hasFired && _hasEnteredSpellAnimation && !isCurrentSpell && !isNextSpell)
            {
                EndSpell();
                return;
            }

            // 회피 캔슬: 원거리 공격을 발사한 이후에는 남은 모션을 끊고 바로 회피 가능
            if (_hasFired && _player.IsGrounded && _player.Movement.CanDodge && _inputHandler.TryUseDodgeInput())
            {
                _stateMachine.ChangeState(_player.DodgeState);
                return;
            }
        }

        if (_elapsedTime >= SpellStateSafetyTimeout)
        {
            if (!_hasFired)
            {
                FireSpell();
            }

            EndSpell();
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

        if (!_hasFired && _player.RangedAttack != null)
        {
            _player.RangedAttack.CancelCharge();
        }
    }

    private void EndSpell()
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

    private void FireSpell()
    {
        _hasFired = true;
        _superArmorElapsedTime = 0f;
        _player.RangedAttack.ReleaseAttack();
    }

    private static bool IsSpellState(AnimatorStateInfo stateInfo)
    {
        int shortHash = stateInfo.shortNameHash;
        return shortHash == AnimHashSpell || shortHash == AnimHashSpellEnd;
    }
}
