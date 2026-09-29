using UnityEngine;

public class EnemyGroundMove : EnemyMoveType
{
    public override void Move(Vector2 direction)
    {
        Rigidbody.linearVelocity = new Vector2(direction.x * Enemy.Data.MoveSpeed, Rigidbody.linearVelocity.y);
    }

    public override void BackMove(Vector2 direction)
    {
        Rigidbody.linearVelocity = new Vector2(-direction.x * Enemy.Data.MoveSpeed, Rigidbody.linearVelocity.y);
    }

    public override void StopMove()
    {
        Rigidbody.linearVelocity = new Vector2(0f, Rigidbody.linearVelocity.y);
    }
}
