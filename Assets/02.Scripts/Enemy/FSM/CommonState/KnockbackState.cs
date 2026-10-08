using UnityEngine;

public class KnockbackState : EnemyState
{
    private readonly Enemy _enemy;
    private HitData _hitData;

    private float _minKnockbackTimer = 0f;
    private const float MinKnockbackDuration = 0.8f;

    public KnockbackState(Enemy enemy, HitData hitData)
    {
        _enemy = enemy;
        _hitData = hitData;
    }

    public override void Enter()
    {
        Debug.Log("적 넉백!");
        _enemy.Animation.PlayHit();
        _enemy.MoveType.StopMove();
        // AddForce(Impulse)는 질량에 따라 튀는 높이가 달라지고 연타 시 속도가 누적되므로 직접 대입
        _enemy.EnemyRb.linearVelocity = Vector2.up * _hitData.KnockbackForce;
        _minKnockbackTimer = MinKnockbackDuration;
    }

    public override void FixedUpdate()
    {
        _enemy.MoveType.KnockbackPhysicsUpdate();
    }

    public override void Update()
    {
        if (_minKnockbackTimer > 0f)
        {
            _minKnockbackTimer -= Time.deltaTime;
            return;
        }

        // 종료 판정은 이동 타입별로 (지상: 착지, 중력 없는 적: 속도 소멸)
        if (!_enemy.MoveType.IsKnockbackFinished()) return;

        _enemy.MoveType.StopMove();
        if (_enemy.Type == EnemyType.Buffer)
        {
            _enemy.StateMachine.ChangeState(new FindEnemyState(_enemy));
            return;
        }

        _enemy.StateMachine.ChangeState(new ChaseState(_enemy));
    }

    public override void Exit()
    {
        Debug.Log("적 넉백 종료!");
    }
}
