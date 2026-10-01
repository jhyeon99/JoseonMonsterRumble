using System;
using UnityEngine;

public class EnemyGroundMove : EnemyMoveType
{
    public override void PatrolIdleMove()
    {
        if (Enemy.MoveArea == null) return;

        Vector2 center = Enemy.MoveArea.StartPosition + Enemy.MoveArea.CenterOffset;
        float halfWidth = Enemy.MoveArea.AreaSize.x * 0.5f;
        float minX = center.x - halfWidth;
        float maxX = center.x + halfWidth;
        float width = maxX - minX;

        if (width <= 0f) return;

        float pingPong = Mathf.PingPong(Time.time * Enemy.Data.MoveSpeed, width);
        float targetX = minX + pingPong;

        float directionX = targetX - Rigidbody.position.x;

        HandleFlip(directionX);

        var targetPos = new Vector2(targetX, Rigidbody.position.y);

        var nextPos =
            Vector2.MoveTowards(Rigidbody.position, targetPos, Enemy.Data.MoveSpeed * Time.fixedDeltaTime);
        Rigidbody.MovePosition(nextPos);
    }

    public override bool IsArrivedAtStart()
    {
        Vector2 startPos = Enemy.MoveArea.StartPosition;
        return Vector2.Distance(Rigidbody.position, startPos) < 0.1f;
    }

    public override void MoveToInitialPosition()
    {
        Vector2 startPos = Enemy.MoveArea.StartPosition;
        var nextPos = Vector2.MoveTowards(Rigidbody.position, startPos, Enemy.Data.MoveSpeed * Time.fixedDeltaTime);
        Rigidbody.MovePosition(nextPos);
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

    private void HandleFlip(float directionX)
    {
        if (Mathf.Abs(directionX) < 0.01f) return;

        Vector3 scale = transform.localScale;
        if (directionX > 0f)
        {
            scale.x = Mathf.Abs(scale.x);
        }
        else if (directionX < 0f)
        {
            scale.x = -Mathf.Abs(scale.x);
        }

        transform.localScale = scale;
    }
}
