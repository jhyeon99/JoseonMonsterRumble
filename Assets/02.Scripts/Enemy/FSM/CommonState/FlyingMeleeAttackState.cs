using UnityEngine;

public sealed class FlyingMeleeAttackState : EnemyState
{
    private const float MinimumTravelDistance = 0.1f;
    private readonly Enemy _enemy;
    private readonly FlyingMeleeAttackController _attack;
    private readonly EnemyMeleeCombat _combat;
    private readonly EnemyFlyingMove _flight;
    private FlyingMeleeAttackTimeline _timeline;
    private FlyingMeleeAttackPattern _pattern;
    private FlyingMeleeAttackPath _path;
    private Vector2 _positioningTarget;
    private Vector2 _aim;
    private Vector2 _pathDisplacement;
    private Vector2 _lastRequestedPosition;
    private float _entrySide;
    private float _prepareRetreatDistance;
    private bool _skipHover;

    public FlyingMeleeAttackState(Enemy enemy, FlyingMeleeAttackController attack)
    {
        _enemy = enemy;
        _attack = attack;
        _combat = (EnemyMeleeCombat)enemy.Combat;
        _flight = (EnemyFlyingMove)enemy.MoveType;
    }

    public override void Enter()
    {
        _enemy.DirectionLock.LockDirection();
        _flight.ResetMeleeFlight();
        _combat.StopAttack();
        _attack.ResetNavigationProgress();
        _skipHover = _attack.ConsumeImmediateAttack();
        _pattern = _attack.Data.SelectPattern(Random.value);
        _timeline = new FlyingMeleeAttackTimeline(_attack.Data, _pattern);
        _aim = _attack.GetTargetPosition();
        _entrySide = _enemy.EnemyRb.position.x <= _aim.x ? -1f : 1f;
        // 가까울 때만 진입 공간을 확보한다. 준비 위치는 한 번 정하며 표적을 따라 매 프레임 옮기지 않는다.
        _positioningTarget = _enemy.EnemyRb.position;
        if (Mathf.Abs(_positioningTarget.x - _aim.x) < _attack.Data.ApproachDistance)
        {
            _positioningTarget.x = _aim.x + _entrySide * _attack.Data.ApproachDistance;
        }
        _positioningTarget.y = Mathf.Max(_positioningTarget.y, _aim.y + _attack.Data.DiveHeight);
        _positioningTarget = _attack.ClampPosition(_positioningTarget);
        if (_pattern == FlyingMeleeAttackPattern.Horizontal)
        {
            float flightHeight = _attack.GetHorizontalFlightHeight();
            var horizontalStart = new Vector2(_positioningTarget.x, flightHeight);
            var end = new Vector2(_aim.x - _entrySide * _attack.Data.ApproachDistance, flightHeight);
            if (_attack.CanTravelStraight(horizontalStart, end) &&
                _attack.CanTravelStraight(_enemy.EnemyRb.position, horizontalStart))
            {
                _positioningTarget = horizontalStart;
            }
            else
            {
                _pattern = FlyingMeleeAttackPattern.Dive;
                _timeline = new FlyingMeleeAttackTimeline(_attack.Data, _pattern);
            }
        }
        _pathDisplacement = Vector2.zero;
        _combat.OnAttackHit += HandleAttackHit;
        _attack.PlayPhase(FlyingMeleeAttackPhase.Positioning);
    }

    public override void Update()
    {
        if (_enemy.Target == null)
        {
            _enemy.StateMachine.ChangeState(new AlertState(_enemy, _aim));
        }
    }

    public override void FixedUpdate()
    {
        if (_enemy.Target == null)
        {
            _enemy.StateMachine.ChangeState(new AlertState(_enemy, _aim));
            return;
        }

        switch (_timeline.Phase)
        {
            case FlyingMeleeAttackPhase.Positioning:
                UpdatePositioning();
                break;
            case FlyingMeleeAttackPhase.Hovering:
                _flight.HoverInPlace();
                if (_timeline.Advance(Time.fixedDeltaTime))
                {
                    StartApproach();
                }
                break;
            case FlyingMeleeAttackPhase.Preparing:
                if (_timeline.PrepareProgress >= 1f)
                {
                    // 마지막 준비 이동이 물리 단계에서 반영된 뒤 돌진 판정을 시작한다.
                    SetPhase(FlyingMeleeAttackPhase.Striking);
                    _attack.DisableContactDamage();
                    _combat.StartControlledAttack();
                    UpdateStrike();
                    break;
                }
                _timeline.Advance(Time.fixedDeltaTime);
                UpdateParryWarning();
                if (!MoveAlongPath(_timeline.TravelProgress))
                {
                    StartRecovery();
                }
                break;
            case FlyingMeleeAttackPhase.Striking:
                UpdateStrike();
                break;
            case FlyingMeleeAttackPhase.Recovering:
                _flight.HoverInPlace();
                if (_timeline.Advance(Time.fixedDeltaTime))
                {
                    FinishAttack();
                }
                break;
            case FlyingMeleeAttackPhase.Completed:
                FinishAttack();
                break;
        }
    }

    private void UpdatePositioning()
    {
        _timeline.Advance(Time.fixedDeltaTime);
        if (!_attack.IsAvoidingObstacle && _attack.IsAtPosition(_positioningTarget))
        {
            bool canAttackImmediately = _attack.ConsumeImmediateAttack();
            if (_pattern == FlyingMeleeAttackPattern.Horizontal || _skipHover || canAttackImmediately)
            {
                StartApproach();
            }
            else
            {
                SetPhase(FlyingMeleeAttackPhase.Hovering);
            }
        }
        else if (_timeline.HasPositioningTimedOut || !_attack.TryMoveToPosition(_positioningTarget))
        {
            // 막힌 위치에서 추적과 공격 진입을 매 물리 프레임 반복하지 않는다.
            StartRecovery();
        }
    }

    private void StartApproach()
    {
        Vector2 start = _enemy.EnemyRb.position;
        _aim = _attack.GetTargetPosition();
        if (_pattern == FlyingMeleeAttackPattern.Horizontal)
        {
            _aim.y = start.y;
        }
        _entrySide = start.x <= _aim.x ? -1f : 1f;
        // 두 패턴 모두 명중점을 통과한 뒤 반대편까지 비행한다.
        Vector2 end = _attack.ClampPosition(_aim +
            new Vector2(-_entrySide * _attack.Data.ApproachDistance,
                _pattern == FlyingMeleeAttackPattern.Dive ? _attack.Data.DiveHeight : 0f));
        if (_pattern == FlyingMeleeAttackPattern.Horizontal)
        {
            end.y = start.y;
            if (!_attack.CanTravelStraight(start, end))
            {
                _attack.RequestObstacleAvoidance(end - start);
                StartRecovery();
                return;
            }
        }
        // 영역 경계에 붙어 돌진할 공간이 없으면 억지로 되감는 곡선을 만들지 않는다.
        float strikeDirection = -_entrySide;
        if ((_aim.x - start.x) * strikeDirection < MinimumTravelDistance ||
            (end.x - _aim.x) * strikeDirection < MinimumTravelDistance)
        {
            _attack.RequestObstacleAvoidance(_aim - start);
            StartRecovery();
            return;
        }
        _path = new FlyingMeleeAttackPath(_pattern, start, _aim, end);
        _prepareRetreatDistance = _attack.CalculateAllowedPrepareRetreatDistance(-_path.CalculateDirection(0f));
        _lastRequestedPosition = start;
        _pathDisplacement = Vector2.zero;
        SetPhase(FlyingMeleeAttackPhase.Preparing);
        UpdateParryWarning();
    }

    private void UpdateParryWarning()
    {
        float remaining = _attack.Data.GetTiming(_pattern).PrepareTime * (1f - _timeline.PrepareProgress);
        _attack.UpdateParryWarning(remaining);
    }

    private void UpdateStrike()
    {
        if (_timeline.StrikeProgress >= 1f)
        {
            StartRecovery();
            return;
        }
        _timeline.Advance(Time.fixedDeltaTime);
        if (!MoveAlongPath(_timeline.TravelProgress))
        {
            StartRecovery();
        }
    }

    private bool MoveAlongPath(float progress)
    {
        // 약한 밀림 중 중단된 뒤에도 실제 변위를 반영하여 이전 좌표로 되돌아가지 않는다.
        _pathDisplacement += _enemy.EnemyRb.position - _lastRequestedPosition;
        Vector2 nextPosition = _path.CalculatePosition(progress) + _pathDisplacement;
        if (_timeline.Phase == FlyingMeleeAttackPhase.Preparing)
        {
            nextPosition += _path.CalculatePrepareRetreatOffset(_timeline.PrepareProgress, _prepareRetreatDistance);
        }
        bool canMove = _attack.TryMove(nextPosition, _path.CalculateDirection(progress));
        if (!canMove)
        {
            _attack.RequestObstacleAvoidance(nextPosition - _enemy.EnemyRb.position);
        }
        _lastRequestedPosition = nextPosition;
        return canMove;
    }

    private void StartRecovery()
    {
        _combat.StopAttack();
        _attack.RestoreContactDamage();
        _positioningTarget = _enemy.EnemyRb.position;
        SetPhase(FlyingMeleeAttackPhase.Recovering);
    }

    private void SetPhase(FlyingMeleeAttackPhase phase)
    {
        _timeline.SetPhase(phase);
        _attack.PlayPhase(phase);
    }

    private void HandleAttackHit()
    {
        _timeline.ApplyHitSlowdown();
    }

    private void FinishAttack()
    {
        _timeline.SetPhase(FlyingMeleeAttackPhase.Completed);
        if (_enemy.Target != null && _enemy.MoveArea.IsPositionInChaseArea(_enemy.Target.position))
        {
            _enemy.StateMachine.ChangeState(new ChaseState(_enemy));
        }
        else
        {
            _enemy.StateMachine.ChangeState(new AlertState(_enemy, _aim));
        }
    }

    public override void Exit()
    {
        _combat.OnAttackHit -= HandleAttackHit;
        _combat.StopAttack();
        _attack.RestoreContactDamage();
        _attack.StopAttackPresentation();
        _flight.ResetMeleeFlight();
        _enemy.DirectionLock.UnlockDirection();
        _timeline.SetPhase(FlyingMeleeAttackPhase.Completed);
        _path = default;
        _pathDisplacement = Vector2.zero;
        _lastRequestedPosition = Vector2.zero;
        _positioningTarget = Vector2.zero;
        _aim = Vector2.zero;
        _entrySide = 0f;
        _prepareRetreatDistance = 0f;
    }
}
