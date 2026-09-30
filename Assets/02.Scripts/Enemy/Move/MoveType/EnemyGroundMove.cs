using System;
using UnityEngine;

public class EnemyGroundMove : EnemyMoveType
{
    private void FixedUpdate()
    {
        float yBound = Enemy.Target.position.y;

        Vector2 clampedPosition = Enemy.EnemyRb.position;
        clampedPosition.y = Mathf.Max(yBound, Enemy.EnemyRb.position.y);
        Enemy.EnemyRb.position = clampedPosition;
    }

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
