using UnityEngine;

public class PlayerSpellState : PlayerBaseState
{
    private const float CastFireTiming = 0.6875f;
    private const float TotalSpellDuration = 1.25f;
    private const string AnimStateSpell = "PlayerSpell";
    private const string AnimStateSpellEnd = "PlayerSpellEnd";

    private float _elapsedTime;
    private bool _hasFired;
    private bool _hasEnteredSpellAnimation;
    private RigidbodyConstraints2D _originalConstraints;

    public PlayerSpellState(Player player, StateMachine stateMachine)
        : base(player, stateMachine)
    {
    }

    public override void Enter()
    {
        base.Enter();

        _elapsedTime = 0f;
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

        if (!_hasFired && _elapsedTime >= CastFireTiming)
        {
            _hasFired = true;
            _player.RangedAttack.FireSingleSpell();
        }

        if (_elapsedTime >= TotalSpellDuration)
        {
            EndSpell();
            return;
        }

        Animator animator = _player.Animator;
        if (animator != null)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

            if (stateInfo.IsName(AnimStateSpell) || stateInfo.IsName(AnimStateSpellEnd))
            {
                _hasEnteredSpellAnimation = true;
            }

            if (_hasEnteredSpellAnimation &&
                !animator.IsInTransition(0) &&
                !stateInfo.IsName(AnimStateSpell) &&
                !stateInfo.IsName(AnimStateSpellEnd))
            {
                EndSpell();
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
}
