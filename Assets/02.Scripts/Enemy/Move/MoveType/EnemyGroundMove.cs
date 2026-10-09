using UnityEngine;

public class EnemyGroundMove : EnemyMoveType
{
    private const float GroundNormalThreshold = 0.7f;
    private const float LandingVelocityThreshold = 0.1f;

    private readonly ContactPoint2D[] _groundContacts = new ContactPoint2D[8];
    private ContactFilter2D _groundFilter;

    protected override void Awake()
    {
        base.Awake();

        _groundFilter.SetLayerMask(Constants.Layers.GroundAllMask);
        _groundFilter.useTriggers = false;
    }

    /// <summary>PlayerMovement.IsGrounded와 같은 방식 (바닥 방향 접촉 + 실제로 받쳐주는 접촉만)</summary>
    public bool IsGrounded()
    {
        int count = Rigidbody.GetContacts(_groundFilter, _groundContacts);
        for (int i = 0; i < count; i++)
        {
            ContactPoint2D contact = _groundContacts[i];

            if (contact.normal.y <= GroundNormalThreshold) continue;
            if (contact.normalImpulse <= 0f) continue;

            return true;
        }

        return false;
    }

    public override void KnockbackPhysicsUpdate(bool isHorizontalOnly)
    {
        if (!isHorizontalOnly)
        {
            return;
        }

        UpdateHorizontalKnockback();
    }

    public override bool IsKnockbackFinished(bool isHorizontalOnly)
    {
        if (isHorizontalOnly)
        {
            return IsHorizontalKnockbackStopped();
        }

        return IsGrounded() && Rigidbody.linearVelocity.y <= LandingVelocityThreshold;
    }

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

    public override void MoveToInitialPosition()
    {
        Vector2 startPos = Enemy.MoveArea.StartPosition;
        float directionX = startPos.x - Rigidbody.position.x;
        HandleFlip(directionX);
        var nextPos = Vector2.MoveTowards(Rigidbody.position, startPos, Enemy.Data.MoveSpeed * Time.fixedDeltaTime);
        Rigidbody.MovePosition(nextPos);
    }

    public override void Move(Vector2 direction)
    {
        IsBackMove = false;
        Rigidbody.linearVelocity = new Vector2(direction.x * Enemy.Data.MoveSpeed, Rigidbody.linearVelocity.y);
    }

    public override void BackMove(Vector2 direction)
    {
        IsBackMove = true;
        Rigidbody.linearVelocity = new Vector2(-direction.x * Enemy.Data.MoveSpeed, Rigidbody.linearVelocity.y);
    }

    public override bool IsEndPosition(Vector2 direction)
    {
        float nextX = Rigidbody.position.x - direction.x * Enemy.Data.MoveSpeed * Time.fixedDeltaTime;
        float clampX = Enemy.MoveArea.ClampX(nextX);

        if (Mathf.Abs(nextX - clampX) > 0.0001f)
        {
            return true;
        }

        return false;
    }

    public override void StopMove()
    {
        IsBackMove = false;
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
