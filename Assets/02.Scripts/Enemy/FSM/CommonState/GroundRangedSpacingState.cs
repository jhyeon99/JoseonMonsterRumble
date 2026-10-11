using UnityEngine;

/// <summary>안전거리를 확보한 뒤 공격한다. 방향 반전은 정지 완료 후에만 허용한다.</summary>
public class GroundRangedSpacingState : EnemyState
{
    private enum MotionPhase { Fleeing, StoppingToReverse, StoppingToAttack }

    private const float BreakoutSpeedMultiplier = 1.6f;

    private readonly Enemy _enemy;
    private readonly EnemyGroundMove _move;
    private MotionPhase _phase;
    private float _direction;
    private bool _hasReversed;
    private float _breakoutPlayerStartX;

    public GroundRangedSpacingState(Enemy enemy)
    {
        _enemy = enemy;
        _move = (EnemyGroundMove)enemy.MoveType;
    }

    public override void Enter()
    {
        float targetX = _enemy.Target != null ? _enemy.Target.position.x : _enemy.transform.position.x;
        _direction = targetX >= _enemy.transform.position.x ? -1f : 1f;
        _phase = MotionPhase.Fleeing;
        _hasReversed = false;
        _breakoutPlayerStartX = 0f;
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
        if (_phase == MotionPhase.Fleeing)
        {
            bool shouldStop = _hasReversed
                ? (_enemy.transform.position.x - _breakoutPlayerStartX) * _direction >= _enemy.Data.RangedFleeEndDistance
                : _enemy.GetDistanceXToPlayer() >= _enemy.Data.RangedFleeEndDistance;

            if (shouldStop)
            {
                StartStopping(MotionPhase.StoppingToAttack);
            }
        }
        if (_phase == MotionPhase.Fleeing || !_move.IsWalkStopped)
        {
            return;
        }
        if (_phase == MotionPhase.StoppingToReverse)
        {
            if (!_hasReversed && _move.CanMoveInDirection(-_direction))
            {
                _direction = -_direction;
                _hasReversed = true;
                _breakoutPlayerStartX = _enemy.Target != null ? _enemy.Target.position.x : _enemy.transform.position.x;
                _phase = MotionPhase.Fleeing;
                _enemy.Animation.SetAnimationSpeed(BreakoutSpeedMultiplier);
                return;
            }

            StartStopping(MotionPhase.StoppingToAttack);
            return;
        }

        _move.FaceTarget();

        if (_enemy.GetDistanceToPlayer() <= _enemy.Data.AttackDetectRange)
        {
            if (_enemy.GetDistanceToPlayer() >= _enemy.Data.RangedFleeEndDistance &&
                Random.value < _enemy.Data.ProvokeChance)
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
            if (_hasReversed)
            {
                StartStopping(MotionPhase.StoppingToAttack);
                return;
            }

            StartStopping(MotionPhase.StoppingToReverse);
            return;
        }

        float speedMultiplier = _hasReversed ? BreakoutSpeedMultiplier : 1f;
        _move.Move(new Vector2(_direction * speedMultiplier, 0f));
    }

    private void StartStopping(MotionPhase phase)
    {
        _phase = phase;
        _enemy.Animation.ResetAnimationSpeed();
        _move.RequestStopMove();
    }

    public override void Exit()
    {
        _direction = 0f;
        _phase = MotionPhase.Fleeing;
        _hasReversed = false;
        _breakoutPlayerStartX = 0f;
        _enemy.Animation.ResetAnimationSpeed();
        _enemy.DirectionLock.UnlockDirection();
    }
}
