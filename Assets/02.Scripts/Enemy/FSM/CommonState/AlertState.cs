using UnityEngine;

public class AlertState : EnemyState
{
    private const float MinAlertDuration = 2f;
    private const float MaxAlertDuration = 5f;

    private enum AlertPhase { Approaching, Stopping, Waiting }

    private readonly Enemy _enemy;
    private readonly Vector2 _searchPosition;
    private AlertPhase _phase;
    private float _remainingTime;

    public AlertState(Enemy enemy) : this(enemy,
        enemy.Target != null ? (Vector2)enemy.Target.position : (Vector2)enemy.transform.position)
    {
    }

    public AlertState(Enemy enemy, Vector2 searchPosition)
    {
        _enemy = enemy;
        _searchPosition = enemy.MoveArea != null ? enemy.MoveArea.ClampPosition(searchPosition) : searchPosition;
    }

    public override void Enter()
    {
        _phase = AlertPhase.Approaching;
        _remainingTime = 0f;
        _enemy.DirectionLock.LockDirection();
    }

    public override void Update()
    {
        if (_enemy.MoveArea != null && _enemy.Target != null &&
            _enemy.MoveArea.IsPositionDetected(_enemy.Target.position))
        {
            _enemy.StateMachine.ChangeState(new ChaseState(_enemy));
            return;
        }

        if (_phase == AlertPhase.Approaching)
        {
            return;
        }

        if (_phase == AlertPhase.Stopping)
        {
            if (_enemy.MoveType is EnemyGroundMove groundMove && !groundMove.IsWalkStopped)
            {
                return;
            }

            _phase = AlertPhase.Waiting;
            _remainingTime = Random.Range(MinAlertDuration, MaxAlertDuration);
            return;
        }

        _remainingTime -= Time.deltaTime;
        if (_remainingTime <= 0f)
        {
            _enemy.StateMachine.ChangeState(new ReturnToStartState(_enemy));
        }
    }

    public override void FixedUpdate()
    {
        if (_phase == AlertPhase.Approaching)
        {
            if (!_enemy.MoveType.TryMoveToSearchPosition(_searchPosition))
            {
                _phase = AlertPhase.Stopping;
                _enemy.Animation.SetRunning(false);
                _enemy.MoveType.RequestStopMove();
            }
        }
        else if (_phase == AlertPhase.Waiting)
        {
            _enemy.MoveType.StopMove();
        }
    }

    public override void Exit()
    {
        _remainingTime = 0f;
        _phase = AlertPhase.Stopping;
    }
}
