using UnityEngine;

public class FindEnemyState : EnemyState
{
    private readonly Enemy _enemy;
    private float _findTimer = 0f;
    private float _findTime = 1.0f;

    public FindEnemyState(Enemy enemy)
    {
        _enemy = enemy;
    }

    public override void Enter()
    {
        _findTimer = _findTime;
        Debug.Log("상태 등장");
        _enemy.MoveType.StopMove();
    }

    public override void Update()
    {
        _findTimer -= Time.deltaTime;
        if (_findTimer <= 0f)
        {
            _enemy.FindNewTarget();
            _findTimer = _findTime;
        }
    }
}
