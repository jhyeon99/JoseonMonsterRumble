using UnityEngine;

public class KnockbackState : EnemyState
{
    private readonly Enemy _enemy;
    private HitData _hitData;
    private float _knockbackTimer = 0;
    private float _knockbackTime = 1.5f;

    public KnockbackState(Enemy enemy, HitData hitData)
    {
        _enemy = enemy;
        _hitData = hitData;
    }

    public override void Enter()
    {
        _knockbackTimer = _knockbackTime;
        Debug.Log("적 넉백!");
        _enemy.MoveType.StopMove();
        _enemy.EnemyRb.AddForce(_hitData.AttackDirection * _hitData.KnockbackForce, ForceMode2D.Impulse);
    }

    public override void Update()
    {
        _knockbackTimer -= Time.deltaTime;
        if (_knockbackTimer <= 0f)
        {
            _enemy.MoveType.StopMove();
            if (_enemy.Type == EnemyType.Buffer)
            {
                _enemy.StateMachine.ChangeState(new FindEnemyState(_enemy));
                return;
            }

            _enemy.StateMachine.ChangeState(new ChaseState(_enemy));
        }
    }

    public override void Exit()
    {
        Debug.Log("적 넉백 종료!");
    }
}
