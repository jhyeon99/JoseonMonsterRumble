using UnityEngine;

public class EnragedState : EnemyState
{
    private Enemy _enemy;

    public EnragedState(Enemy enemy)
    {
        _enemy = enemy;
    }

    public override void Enter()
    {
        Debug.Log("도깨비불 광폭화!!");
    }

    public override void FixedUpdate()
    {
        _enemy.MoveType.Move(_enemy.GetDirectionToPlayer());
    }

    public override void Update()
    {
    }
}
