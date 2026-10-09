using UnityEngine;

public class PlayerHitState : PlayerBaseState
{
    private RigidbodyConstraints2D _originalConstraints;
    private bool _hasEnteredHitAnimation;
    private float _elapsedTime;
    private const float SafetyTimeout = 2.5f;

    private HitData _hitData;
    private bool _hasKnockback;

    public override PlatformInteractionMode PlatformMode =>
        _hasKnockback ? PlatformInteractionMode.FreeTrajectory : PlatformInteractionMode.Carried;

    public PlayerHitState(Player player, StateMachine stateMachine)
        : base(player, stateMachine)
    {
    }

    public void SetHitData(in HitData hitData)
    {
        _hitData = hitData;
    }

    public override void Enter()
    {
        base.Enter();

        _elapsedTime = 0f;
        _hasEnteredHitAnimation = false;
        _hasKnockback = _hitData.KnockbackForce > 0f;

        _player.SetRunning(false);
        _player.TriggerHit();

        if (_hasKnockback)
        {
            float dirX = _hitData.AttackDirection.x >= 0f ? 1f : -1f;

            // 때린 쪽을 바라보도록
            _player.transform.localScale = new Vector3(-dirX, 1f, 1f);

            // 무빙 플랫폼에서 떨어져 나가도록 (JumpState와 동일)
            _player.Movement.SetMovingPlatform(null);

            float horizontalSpeed = Mathf.Clamp(
                _hitData.KnockbackForce * _player.Movement.KnockbackHorizontalMultiplier,
                5f,
                9f
            );

            _rigidbody2D.linearVelocity = new Vector2(
                dirX * horizontalSpeed,
                _player.Movement.KnockbackUpVelocity);
        }
        else
        {
            // 넉백 없는 피격: 제자리 경직
            _originalConstraints = _rigidbody2D.constraints;
            _rigidbody2D.constraints = _originalConstraints | RigidbodyConstraints2D.FreezePositionX;
            _rigidbody2D.linearVelocity = new Vector2(0f, _rigidbody2D.linearVelocity.y);
        }
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

        if (_hasKnockback)
        {
            Vector2 v = _rigidbody2D.linearVelocity;
            v.x = Mathf.MoveTowards(v.x, 0f, _player.Movement.KnockbackDeceleration * Time.fixedDeltaTime);
            _rigidbody2D.linearVelocity = v;
        }
        else
        {
            _player.Movement.MoveX(0f);
        }
    }

    public override void Exit()
    {
        base.Exit();

        if (!_hasKnockback)
        {
            _rigidbody2D.constraints = _originalConstraints;
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
