using UnityEngine;

public class EnemyBuffMove : EnemyMoveType
{
    public override void Move(Vector2 direction)
    {
        Rigidbody.linearVelocity = direction * Enemy.Data.MoveSpeed;
    }

    public override void BackMove(Vector2 direction)
    {
    }

    public override void StopMove()
    {
        Rigidbody.linearVelocity = Vector2.zero;
    }
}
