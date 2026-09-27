using UnityEngine;

public class EnemyFlyingMove : EnemyMoveType
{
    public override void Move(Vector2 direction)
    {
        Rigidbody.linearVelocity = new Vector2(direction.x * Enemy.Data.MoveSpeed, direction.y * Enemy.Data.MoveSpeed);
    }
}