using UnityEngine;

public class ChaseState : EnemyState
{
    private enum ExitMotion { Stop, Continue }

    private readonly Enemy _enemy;
    private Vector2 _lastSeenPosition;
    private ExitMotion _exitMotion;

    public ChaseState(Enemy enemy)
    {
        _enemy = enemy;
    }

    public override void Enter()
    {
        Debug.Log("추적 시작");
        _exitMotion = ExitMotion.Stop;
        _enemy.DirectionLock.UnlockDirection();
        _lastSeenPosition = _enemy.Target != null ? (Vector2)_enemy.Target.position : (Vector2)_enemy.transform.position;
    }

    public override void FixedUpdate()
    {
        if (_enemy.Target == null || (_enemy.MoveType is EnemyGroundMove && (ShouldFlee() || CanStartAttack())))
        {
            return;
        }
        if (_enemy.MoveType is EnemyGroundMove groundMove)
        {
            groundMove.MoveToTargetPosition(_enemy.Target.position);
            return;
        }
        _enemy.Animation.SetRunning(true);
        _enemy.MoveType.Move(_enemy.GetDirectionToPlayer());
    }

    public override void Update()
    {
        if (_enemy.Target == null)
        {
            _enemy.StateMachine.ChangeState(new AlertState(_enemy, _lastSeenPosition));
            return;
        }
        if (_enemy.MoveArea != null && _enemy.Target != null)
        {
            if (!_enemy.MoveArea.IsPositionInChaseArea(_enemy.Target.position))
            {
                // 마지막으로 본 위치를 사용한다. 이동 한계는 Alert의 목표 제한과 이동 코드가 처리한다.
                _exitMotion = ExitMotion.Continue;
                _enemy.StateMachine.ChangeState(new AlertState(_enemy, _lastSeenPosition));
                return;
            }
            _lastSeenPosition = _enemy.Target.position;
        }

        if (ShouldFlee())
        {
            _exitMotion = ExitMotion.Continue;
            _enemy.StateMachine.ChangeState(new GroundRangedSpacingState(_enemy));
            return;
        }
        if (CanStartAttack())
        {
            if (_enemy.MoveType is EnemyGroundMove)
            {
                if (_enemy.Combat is EnemyRangedCombat && Random.value < _enemy.Data.ProvokeChance)
                {
                    _enemy.StateMachine.ChangeState(new EnemyProvokeState(_enemy));
                    return;
                }
                _enemy.StateMachine.ChangeState(AttackPrepareGroundState.CreateAttackEntryState(_enemy));
            }
            else if (_enemy.MoveType is EnemyFlyingMove)
            {
                _enemy.StateMachine.ChangeState(new AttackPrepareFlyingState(_enemy));
            }
        }
    }

    private bool ShouldFlee()
    {
        return _enemy.MoveType is EnemyGroundMove && _enemy.Combat is EnemyRangedCombat &&
            _enemy.Data.RangedFleeStartDistance > 0f &&
            _enemy.GetDistanceXToPlayer() < _enemy.Data.RangedFleeStartDistance;
    }

    private bool CanStartAttack()
    {
        if (_enemy.Combat is EnemyMeleeCombat melee)
        {
            return _enemy.GetDistanceXToPlayer() <= _enemy.Data.AttackDetectRange &&
                (!(_enemy.MoveType is EnemyGroundMove) || melee.IsTargetInAttackHeight(_enemy.Target));
        }
        return _enemy.GetDistanceToPlayer() <= _enemy.Data.AttackDetectRange;
    }

    public override void Exit()
    {
        _lastSeenPosition = Vector2.zero;
        Debug.Log("추적 종료");
        if (_exitMotion == ExitMotion.Stop)
        {
            _enemy.Animation.SetRunning(false);
        }
        _exitMotion = ExitMotion.Stop;
    }
}
