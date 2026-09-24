using UnityEngine;

public class EnemyGroundMove : EnemyMove
{
    public override void Move(Vector2 direction)
    {
        Rigidbody.linearVelocity = new Vector2(direction.x * Enemy.Data.MoveSpeed, Rigidbody.linearVelocity.y);
    }
}