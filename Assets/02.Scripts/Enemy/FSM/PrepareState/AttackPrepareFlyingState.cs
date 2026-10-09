using UnityEngine;

public class AttackPrepareFlyingState : EnemyState
{
    protected Enemy _enemy;
    protected float _attackPrepareTimer = 0;

    public AttackPrepareFlyingState(Enemy enemy)
    {
        _enemy = enemy;
    }

    public override void Enter()
    {
        Debug.Log("적 공격 준비 시작");
        _attackPrepareTimer = _enemy.Data.AttackPrepareTime;
        _enemy.MoveType.StopMove();
        if (_enemy.Combat is EnemyMeleeCombat)
        {
            _enemy.Animation.PlayAttack();
        }
    }

    public override void FixedUpdate()
    {
        if (_enemy.GetDistanceToPlayer() <= _enemy.Data.AttackDetectRange)
        {
            Vector2 retreatDirection = -_enemy.GetDirectionToPlayer();
            bool isBlocked = _enemy.AirSensor != null &&
                !_enemy.AirSensor.CanFlyAhead(_enemy.MoveArea, _enemy.transform.position, retreatDirection, 1.0f);

            if (isBlocked)
            {
                _enemy.Animation.SetRunning(false);
                if (_enemy.MoveType is EnemyFlyingMove flyingMove)
                {
                    flyingMove.HoverInPlace();
                }
                else
                {
                    _enemy.MoveType.StopMove();
                }
            }
            else
            {
                _enemy.Animation.SetRunning(true);
                _enemy.MoveType.BackMove(_enemy.GetDirectionToPlayer());
            }
        }
        else
        {
            _enemy.Animation.SetRunning(false);
            if (_enemy.MoveType is EnemyFlyingMove flyingMove)
            {
                flyingMove.HoverInPlace();
            }
            else
            {
                _enemy.MoveType.StopMove();
            }
        }
    }

    public override void Update()
    {
        _attackPrepareTimer -= Time.deltaTime;
        if (_attackPrepareTimer <= 0)
        {
            Debug.Log("공중 적 공격 준비");
            if (_enemy.Combat is EnemyMeleeCombat && _enemy.Target != null)
            {
                _enemy.MoveType.StartQuadraticCharge(_enemy.Target.position);
            }

            _enemy.StateMachine.ChangeState(new AttackState(_enemy));
        }
    }

    public override void Exit()
    {
        _enemy.MoveType.IsBackMove = false;
        if (_enemy.Combat is EnemyRangedCombat)
        {
            _enemy.MoveType.StopMove();
        }

        Debug.Log("적 공격 준비 완료");
    }
}
