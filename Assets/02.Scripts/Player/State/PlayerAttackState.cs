using UnityEngine;

public class PlayerAttackState : PlayerBaseState
{
    private float _elapsedTime;
    private RigidbodyConstraints2D _originalConstraints;
    private bool _hasEnteredAttackAnimation;

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

        _player.SetRunning(false);
        _player.TriggerAttack();
        _player.Attack.ExecuteAttack();
    }

    public override void Execute()
    {
        base.Execute();

        _elapsedTime += Time.deltaTime;

        float duration = _player.AttackMotion.TotalDuration;
        if (duration <= 0f)
        {
            duration = 2.75f;
        }

        if (_elapsedTime >= duration)
        {
            EndAttack();
            return;
        }

        Animator animator = _player.Animator;
        if (animator != null)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

            if (stateInfo.IsName("Player_Attack"))
            {
                _hasEnteredAttackAnimation = true;
            }

            if (_hasEnteredAttackAnimation && (!stateInfo.IsName("Player_Attack") || stateInfo.normalizedTime >= 1.0f))
            {
                EndAttack();
                return;
            }
        }
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