using UnityEngine;

public class AttackRecoveryState : EnemyState
{
    private enum FlyingRecoveryMode
    {
        DisengageRetreat,
        CircleHover,
        GentleBobbing
    }

    private readonly Enemy _enemy;
    private float _recoveryTimer = 0;
    private FlyingRecoveryMode _flyingRecoveryMode;
    private float _targetRetreatDistance;

    public AttackRecoveryState(Enemy enemy)
    {
        _enemy = enemy;
    }

    public override void Enter()
    {
        Debug.Log("휴식 시작");
        _recoveryTimer = _enemy.Data.AttackRecoveryTime;
        if (_enemy.MoveType is EnemyGroundMove || _enemy.Data.HasStationaryAttackCycle)
        {
            _enemy.MoveType.StopMove();
            _enemy.Animation.SetRunning(false);
            _enemy.DirectionLock.LockDirection();
            return;
        }

        if (_enemy.MoveType is EnemyFlyingMove)
        {
            float roll = Random.value;
            if (roll < 0.40f)
            {
                _flyingRecoveryMode = FlyingRecoveryMode.DisengageRetreat;
                _targetRetreatDistance = Random.Range(7.5f, 8.5f);
            }
            else if (roll < 0.75f)
            {
                _flyingRecoveryMode = FlyingRecoveryMode.CircleHover;
            }
            else
            {
                _flyingRecoveryMode = FlyingRecoveryMode.GentleBobbing;
            }
        }
    }

    public override void FixedUpdate()
    {
        if (_enemy.MoveType is EnemyGroundMove || _enemy.Data.HasStationaryAttackCycle)
        {
            return;
        }

        if (_enemy.MoveType is EnemyFlyingMove flyingMove)
        {
            HandleFlyingRecoveryPhysics(flyingMove);
        }
    }

    private void HandleFlyingRecoveryPhysics(EnemyFlyingMove flyingMove)
    {
        Vector2 toPlayerDir = _enemy.GetDirectionToPlayer();
        Vector2 retreatDir = -toPlayerDir;

        bool isBlocked = _enemy.AirSensor != null &&
            !_enemy.AirSensor.CanFlyAhead(_enemy.MoveArea, _enemy.transform.position, retreatDir, 1.0f);

        if (isBlocked || flyingMove.IsEndPosition(toPlayerDir))
        {
            _enemy.Animation.SetRunning(false);
            flyingMove.HoverInPlace();
            return;
        }

        switch (_flyingRecoveryMode)
        {
            case FlyingRecoveryMode.DisengageRetreat:
                if (_enemy.GetDistanceToPlayer() < _targetRetreatDistance)
                {
                    _enemy.Animation.SetRunning(true);
                    flyingMove.MoveDisengage(toPlayerDir);
                }
                else
                {
                    _enemy.Animation.SetRunning(false);
                    flyingMove.HoverInPlace();
                }
                break;

            case FlyingRecoveryMode.CircleHover:
                _enemy.Animation.SetRunning(true);
                flyingMove.MoveCircleHover(toPlayerDir);
                break;

            case FlyingRecoveryMode.GentleBobbing:
            default:
                _enemy.Animation.SetRunning(false);
                flyingMove.HoverInPlace();
                break;
        }
    }

    public override void Update()
    {
        _recoveryTimer -= Time.deltaTime;
        if (_enemy.MoveArea != null && _enemy.Target != null)
        {
            if (!_enemy.MoveArea.IsPositionInChaseArea(_enemy.Target.position))
            {
                _enemy.DirectionLock.LockDirection();
            }
        }

        if (_recoveryTimer <= 0)
        {
            if (_enemy.MoveArea != null &&
                _enemy.Target != null && !_enemy.MoveArea.IsPositionInChaseArea(_enemy.Target.position))
            {
                _enemy.StateMachine.ChangeState(new AlertState(_enemy));
            }
            else
            {
                _enemy.StateMachine.ChangeState(new ChaseState(_enemy));
            }
        }
    }

    public override void Exit()
    {
        Debug.Log("휴식 종료");
        _enemy.MoveType.IsBackMove = false;
        _recoveryTimer = 0f;
        if (_enemy.MoveType is EnemyGroundMove)
        {
            return;
        }
        if (_enemy.MoveArea != null && _enemy.Target != null)
        {
            if (!_enemy.MoveArea.IsPositionInChaseArea(_enemy.Target.position))
            {
                _enemy.transform.rotation = Quaternion.identity;
                Vector3 scale = _enemy.transform.localScale;
                scale.y = 1f;
                _enemy.transform.localScale = scale;
            }
        }
    }
}
