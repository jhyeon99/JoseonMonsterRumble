using UnityEngine;

/// <summary>플레이어와 거리 유지 중 낮은 확률로 시전하는 도발 상태</summary>
public class EnemyProvokeState : EnemyState
{
    private const float ProvokeDuration = 2.6875f;
    private readonly Enemy _enemy;
    private float _timer;

    public EnemyProvokeState(Enemy enemy)
    {
        _enemy = enemy;
    }

    public override void Enter()
    {
        Debug.Log("적 도발 상태 진입");
        _timer = ProvokeDuration;

        _enemy.MoveType.StopMove();
        _enemy.Animation.SetRunning(false);

        if (_enemy.MoveType is EnemyGroundMove groundMove)
        {
            groundMove.FaceTarget();
        }

        _enemy.DirectionLock.LockDirection();
        _enemy.Animation.PlayProvoke();
    }

    public override void Update()
    {
        if (_enemy.Target == null || (_enemy.MoveArea != null &&
            !_enemy.MoveArea.IsPositionInChaseArea(_enemy.Target.position)))
        {
            _enemy.StateMachine.ChangeState(new AlertState(_enemy));
            return;
        }

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            Debug.Log("도발 완료, 공격 상태로 즉시 전이");
            _enemy.StateMachine.ChangeState(AttackPrepareGroundState.CreateAttackEntryState(_enemy));
        }
    }

    public override void Exit()
    {
        _enemy.Animation.ResetProvokeTrigger();
        _timer = 0f;
        Debug.Log("적 도발 상태 종료");
    }
}
