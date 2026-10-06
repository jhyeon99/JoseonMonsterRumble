using UnityEngine;

public class EnemyFlyingMove : EnemyMoveType
{
    public override void Move(Vector2 direction)
    {
        Rigidbody.linearVelocity = direction * Enemy.Data.MoveSpeed;
    }

    public override void BackMove(Vector2 direction)
    {
        Rigidbody.linearVelocity = -(direction * Enemy.Data.MoveSpeed);
    }

    public override void StopMove()
    {
        Rigidbody.linearVelocity = Vector2.zero;
    }

    public override void PatrolIdleMove()
    {
        if (Enemy.MoveArea == null) return;

        Vector2 center = Enemy.MoveArea.StartPosition + Enemy.MoveArea.CenterOffset;

        float halfWidth = (Enemy.MoveArea.AreaSize.x * 0.5f);
        float halfHeight = (Enemy.MoveArea.AreaSize.y * 0.5f);

        float areaScale = Mathf.Max((halfWidth + halfHeight) * 0.5f, 0.1f);

        float t = (Time.time * Enemy.Data.MoveSpeed) / areaScale;

        float x = center.x + (halfWidth * Mathf.Cos(t));
        float y = center.y + (halfHeight * Mathf.Sin(2f * t));

        var targetPos = new Vector2(x, y);

        Vector2 moveVector = targetPos - Rigidbody.position;
        HandleRotationAndFlip(moveVector);

        var nextPos =
            Vector2.MoveTowards(Rigidbody.position, targetPos, Enemy.Data.MoveSpeed * Time.fixedDeltaTime);
        Rigidbody.MovePosition(nextPos);
    }

    public override void MoveToInitialPosition()
    {
        Vector2 startPos = Enemy.MoveArea.StartPosition;

        Vector2 direction = (startPos - Rigidbody.position).normalized;

        Rigidbody.linearVelocity = direction * Enemy.Data.MoveSpeed;

        HandleRotationAndFlip(direction);
    }

    private void HandleRotationAndFlip(Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        transform.rotation = Quaternion.Euler(0f, 0f, angle - 180f);
    }
}
