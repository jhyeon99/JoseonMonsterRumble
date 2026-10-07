using UnityEngine;

public class AttackRecoveryState : EnemyState
{
    private readonly Enemy _enemy;
    private float _recoveryTimer = 0;

    public AttackRecoveryState(Enemy enemy)
    {
        _enemy = enemy;
    }

    public override void Enter()
    {
        Debug.Log("휴식 시작");
        _recoveryTimer = _enemy.Data.AttackRecoveryTime;
    }

    public override void FixedUpdate()
    {
        if (_enemy.GetDistanceToPlayer() <= _enemy.Data.AttackRecoverRange)
        {
            _enemy.Animation.SetRunning(true);
            _enemy.MoveType.BackMove(_enemy.GetDirectionToPlayer());
        }
        else
        {
            _enemy.Animation.SetRunning(false);
            _enemy.MoveType.StopMove();
        }
    }

    public override void Update()
    {
        _recoveryTimer -= Time.deltaTime;
        if (!_enemy.MoveArea.IsPositionInArea(_enemy.Target.position))
        {
            _enemy.DirectionLock.LockDirection();
        }

        if (_recoveryTimer <= 0)
        {
            _enemy.StateMachine.ChangeState(new ChaseState(_enemy));
        }
    }

    public override void Exit()
    {
        Debug.Log("휴식 종료");
        _enemy.MoveType.IsBackMove = false;
        if (!_enemy.MoveArea.IsPositionInArea(_enemy.Target.position))
        {
            _enemy.transform.rotation = Quaternion.identity;
            Vector3 scale = _enemy.transform.localScale;
            scale.y = 1f;
            _enemy.transform.localScale = scale;
        }
    }
}
