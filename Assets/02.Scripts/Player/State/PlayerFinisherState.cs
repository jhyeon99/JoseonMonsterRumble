using UnityEngine;

/// <summary>
/// 그로기 적 마무리 공격. 3타 모션을 바로 재생하고 타격 순간에 적을 날려 보냄.
/// 마무리 공격 중에는 무적.
/// </summary>
public class PlayerFinisherState : PlayerBaseState
{
    private Enemy _target;
    private float _elapsedTime;
    private float _impactDelay;
    private float _duration;
    private bool _hasImpacted;
    private RigidbodyConstraints2D _originalConstraints;

    public PlayerFinisherState(Player player, StateMachine stateMachine)
        : base(player, stateMachine)
    {
    }

    public void SetTarget(Enemy target)
    {
        _target = target;
    }

    public override void Enter()
    {
        base.Enter();

        _elapsedTime = 0f;
        _hasImpacted = false;
        _player.Finisher.GetTiming(_player.Animator, _player.Attack, out _impactDelay, out _duration);

        _originalConstraints = _rigidbody2D.constraints;
        _rigidbody2D.constraints = _originalConstraints | RigidbodyConstraints2D.FreezePositionX;
        _rigidbody2D.linearVelocity = new Vector2(0f, _rigidbody2D.linearVelocity.y);

        _player.SetRunning(false);
        FaceTarget();

        // 마무리 공격 중에는 무적 (반투명 표시 없이)
        _player.Health.SetInvincible(true, false);

        // 준비 동작 중에 그로기가 풀리지 않게 고정
        if (_target != null)
        {
            _target.TryHoldGroggy();
        }

        _player.PlayFinisherAnimation();
    }

    public override void Execute()
    {
        base.Execute();

        _elapsedTime += Time.deltaTime;

        if (!_hasImpacted && _elapsedTime >= _impactDelay)
        {
            Impact();
        }

        if (_elapsedTime >= _duration)
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
        _player.Health.SetInvincible(false, false);

        // 타격 전에 끊겼으면 그로기 고정 해제
        if (!_hasImpacted && _target != null)
        {
            _target.ReleaseGroggy();
        }

        _target = null;
    }

    private void Impact()
    {
        _hasImpacted = true;

        if (_target == null || !_target.isActiveAndEnabled || !_target.IsGroggy)
        {
            return;
        }

        float directionX = _target.transform.position.x >= _player.transform.position.x ? 1f : -1f;
        Vector2 point = _target.transform.position;

        _target.Launch(_player.Finisher.Launch, directionX);
        GameEvents.TriggerFinisher(point);
    }

    private void FaceTarget()
    {
        if (_target == null)
        {
            return;
        }

        float diff = _target.transform.position.x - _player.transform.position.x;
        if (Mathf.Abs(diff) > 0.01f)
        {
            _player.transform.localScale = new Vector3(Mathf.Sign(diff), 1f, 1f);
        }
    }
}
