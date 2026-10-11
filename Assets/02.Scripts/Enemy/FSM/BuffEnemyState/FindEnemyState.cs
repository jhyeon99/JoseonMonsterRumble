using UnityEngine;

public class FindEnemyState : EnemyState
{
    private readonly Enemy _enemy;
    private float _findTimer = 0f;
    private const float FindInterval = 0.5f;

    public FindEnemyState(Enemy enemy)
    {
        _enemy = enemy;
    }

    public override void Enter()
    {
        _enemy.DirectionLock.UnlockDirection();
        _findTimer = FindInterval;
    }

    public override void Update()
    {
        if (EnemyManager.Instance != null && !EnemyManager.Instance.HasAnyAttacker())
        {
            _enemy.StateMachine.ChangeState(new SelfDestructState(_enemy));
            return;
        }

        _findTimer -= Time.deltaTime;
        if (_findTimer <= 0f)
        {
            _findTimer = FindInterval;
            _enemy.FindNewTarget();
        }
    }

    public override void FixedUpdate()
    {
        _enemy.MoveType.PatrolIdleMove();
    }
}
