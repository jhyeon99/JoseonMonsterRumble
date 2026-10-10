using UnityEngine;

/// <summary>3타 피격 이후 안전거리를 확보할 때까지 상승 후퇴한다.</summary>
public class FlyingRangedSpacingState : EnemyState
{
    private readonly Enemy _enemy;
    private readonly EnemyFlyingMove _move;
    private float _retreatDirectionX;

    public FlyingRangedSpacingState(Enemy enemy)
    {
        _enemy = enemy;
        _move = (EnemyFlyingMove)enemy.MoveType;
    }

    public override void Enter()
    {
        _move.StopMove();
        _enemy.DirectionLock.UnlockDirection();
        _retreatDirectionX = _enemy.GetDirectionToPlayer().x >= 0f ? -1f : 1f;
    }

    public override void Update()
    {
        if (_enemy.Target == null || (_enemy.MoveArea != null &&
            !_enemy.MoveArea.IsPositionInChaseArea(_enemy.Target.position)))
        {
            _enemy.StateMachine.ChangeState(new AlertState(_enemy));
            return;
        }

        if (_enemy.GetDistanceToPlayer() >= _enemy.Data.AttackDetectRange)
        {
            ResumeCombat();
        }
    }

    public override void FixedUpdate()
    {
        if (_enemy.Target == null || _enemy.GetDistanceToPlayer() >= _enemy.Data.AttackDetectRange)
        {
            return;
        }

        if (_move.MoveAscendingRetreat(_retreatDirectionX))
        {
            _enemy.Animation.SetRunning(true);
            _enemy.Animation.SetFlightAnimationSpeed(_move.GetAscendingRetreatAnimationSpeed());
        }
        else
        {
            // 이동할 공간이 없는 경우에는 후퇴 상태에 갇히지 않는다.
            ResumeCombat();
        }
    }

    private void ResumeCombat()
    {
        if (_enemy.Target != null && (_enemy.AirSensor == null || _enemy.AirSensor.HasLineOfSight(_enemy.Target.position)))
        {
            _enemy.StateMachine.ChangeState(new AttackPrepareFlyingState(_enemy));
        }
        else
        {
            _enemy.StateMachine.ChangeState(new ChaseState(_enemy));
        }
    }

    public override void Exit()
    {
        _move.StopMove();
        _enemy.Animation.SetRunning(false);
        _enemy.Animation.SetFlightAnimationSpeed(1f);
        _retreatDirectionX = 0f;
    }
}
