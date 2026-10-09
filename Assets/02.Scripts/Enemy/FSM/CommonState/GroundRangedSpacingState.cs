using UnityEngine;

/// <summary>안전거리를 확보한 뒤 공격한다. 방향 반전은 정지 완료 후에만 허용한다.</summary>
public class GroundRangedSpacingState : EnemyState
{
    private enum MotionPhase { Fleeing, StoppingToReverse, StoppingToAttack }

    private readonly Enemy _enemy;
    private readonly EnemyGroundMove _move;
    private MotionPhase _phase;
    private float _direction;

    public GroundRangedSpacingState(Enemy enemy)
    {
        _enemy = enemy;
        _move = (EnemyGroundMove)enemy.MoveType;
    }

    public override void Enter()
    {
        _direction = _enemy.Target.position.x >= _enemy.transform.position.x ? -1f : 1f;
        _phase = MotionPhase.Fleeing;
        _enemy.DirectionLock.LockDirection();
    }

    public override void Update()
    {
        if (_enemy.Target == null || (_enemy.MoveArea != null &&
            !_enemy.MoveArea.IsPositionInChaseArea(_enemy.Target.position)))
        {
            _enemy.StateMachine.ChangeState(new AlertState(_enemy));
            return;
        }
        if (_phase == MotionPhase.Fleeing &&
            _enemy.GetDistanceXToPlayer() >= _enemy.Data.RangedFleeEndDistance)
        {
            StartStopping(MotionPhase.StoppingToAttack);
        }
        if (_phase == MotionPhase.Fleeing || !_move.IsWalkStopped)
        {
            return;
        }
        if (_phase == MotionPhase.StoppingToReverse && _move.CanMoveInDirection(-_direction))
        {
            _direction = -_direction;
            _phase = MotionPhase.Fleeing;
            return;
        }
        // 양쪽 모두 막힌 경우에도 방향 반전을 반복하지 않고 제자리 공격을 선택한다.
        if (_enemy.GetDistanceToPlayer() <= _enemy.Data.AttackDetectRange)
        {
            if (Random.value < _enemy.Data.ProvokeChance)
            {
                _enemy.StateMachine.ChangeState(new EnemyProvokeState(_enemy));
                return;
            }
            _enemy.StateMachine.ChangeState(AttackPrepareGroundState.CreateAttackEntryState(_enemy));
        }
        else
        {
            _enemy.StateMachine.ChangeState(new ChaseState(_enemy));
        }
    }

    public override void FixedUpdate()
    {
        if (_phase != MotionPhase.Fleeing)
        {
            return;
        }
        if (!_move.IsGrounded())
        {
            _move.StopMove();
            _enemy.Animation.SetRunning(false);
            return;
        }
        if (!_move.CanMoveInDirection(_direction))
        {
            StartStopping(MotionPhase.StoppingToReverse);
            return;
        }
        _move.Move(new Vector2(_direction, 0f));
    }

    private void StartStopping(MotionPhase phase)
    {
        _phase = phase;
        _move.RequestStopMove();
    }

    public override void Exit()
    {
        _direction = 0f;
        _phase = MotionPhase.Fleeing;
    }
}
