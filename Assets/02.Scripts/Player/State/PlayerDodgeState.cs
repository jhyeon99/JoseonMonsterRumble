using UnityEngine;

public class PlayerDodgeState : PlayerBaseState
{
    private PlayerDodgeAfterimage _afterimage;
    private float _elapsedTime;
    private float _dodgeDirection;
    private Vector2 _startPosition;
    private RigidbodyConstraints2D _originalConstraints;
    private bool _hasEnteredDodgeAnimation;
    private bool _isInvincibleEnded;
    private bool _isDashEnded;
    private bool _hasTriggeredDodgeSuccess;

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
        _hasTriggeredDodgeSuccess = false;

        _originalConstraints = _rigidbody2D.constraints;

        _player.Health.SetInvincible(true);
        _player.Health.OnDodgeSuccess += HandleDodgeSuccess;
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

        if (_afterimage == null)
        {
            _afterimage = _player.GetComponent<PlayerDodgeAfterimage>();
            if (_afterimage == null)
            {
                _afterimage = _player.gameObject.AddComponent<PlayerDodgeAfterimage>();
            }
        }

        _afterimage.Begin();

        _startPosition = _rigidbody2D.position;
        _rigidbody2D.linearVelocity = new Vector2(0f, _rigidbody2D.linearVelocity.y);
    }

    public override void Execute()
    {
        base.Execute();

        _elapsedTime += Time.deltaTime;
        if (_elapsedTime >= _player.Movement.DodgeDuration)
        {
            _afterimage.Stop();
        }

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

        if (!_hasTriggeredDodgeSuccess && _elapsedTime < _player.Movement.DodgeInvincibleDuration)
        {
            CheckActiveDodgeSuccess();
        }
    }

    private void CheckActiveDodgeSuccess()
    {
        if (_hasTriggeredDodgeSuccess)
        {
            return;
        }

        Collider2D playerCollider = _player.GetComponent<Collider2D>();
        if (playerCollider == null)
        {
            return;
        }

        int mask = LayerMask.GetMask("EnemyAttack", "Enemy");
        var filter = new ContactFilter2D();
        filter.SetLayerMask(mask);
        filter.useTriggers = true;

        var results = new Collider2D[4];
        int count = playerCollider.Overlap(filter, results);
        if (count > 0)
        {
            TriggerSuccessDodgeMotion();
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

        _player.Health.OnDodgeSuccess -= HandleDodgeSuccess;

        if (_afterimage != null)
        {
            _afterimage.Stop();
        }

        _rigidbody2D.constraints = _originalConstraints;
        _player.Health.SetInvincible(false);
        _player.Movement.StartDodgeCooldown();
    }

    private void HandleDodgeSuccess(HitData hitData)
    {
        TriggerSuccessDodgeMotion();
    }

    private void TriggerSuccessDodgeMotion()
    {
        if (_hasTriggeredDodgeSuccess)
        {
            return;
        }

        _hasTriggeredDodgeSuccess = true;
        HitStop.Play(0.18f, 0.01f);
        CameraShake.Shake(new CameraShakeSettings(0.15f, 0.35f));

        if (!AbilityItemProgress.HasItem(AbilityItemProgress.DokkaebiGamtuId))
        {
            return;
        }
        if (_afterimage != null)
        {
            _afterimage.TriggerDodgeSuccessGlow();
        }

        if (_player != null && _player.WeaponBuff != null)
        {
            _player.WeaponBuff.ActivateBuff(4f, 1.30f);
        }
    }
}
