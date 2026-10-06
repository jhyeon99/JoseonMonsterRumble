using UnityEngine;

public class EnemyBuffMove : EnemyMoveType
{
    [SerializeField] private float _lerpValue;
    [Range(0f, 180f)] public float MaxAngle;
    private bool _isChasing;
    private float _chaseTime;

    public override void Move(Vector2 direction)
    {
        Rigidbody.linearVelocity = direction * Enemy.Data.MoveSpeed;
    }

    public override void BackMove(Vector2 direction)
    {
    }

    private Vector2 GetChaseStartPosition(Enemy target)
    {
        float startAngle = MaxAngle;
        float rad = startAngle * Mathf.Deg2Rad;

        float localX = Mathf.Sin(rad) * target.Radius;
        float localY = Mathf.Cos(rad) * target.Radius;

        return target.GetEnemyCenterPosition() + (Vector2.up * localY) + (Vector2.right * localX);
    }

    public override void MoveToStartPosition(Enemy target)
    {
        Vector2 startWorldPos = GetChaseStartPosition(target);
        float direction = target.transform.position.x - transform.position.x;
        Flip(direction);

        var smoothPos =
            Vector2.MoveTowards(Rigidbody.position, startWorldPos, Enemy.Data.MoveSpeed * Time.fixedDeltaTime);
        Rigidbody.MovePosition(smoothPos);
        if (Vector2.Distance(Rigidbody.position, startWorldPos) < 0.1f)
        {
            _chaseTime = 0f;
            _isChasing = false;
            Enemy.StateMachine.ChangeState(new BuffState(Enemy, target));
        }
    }

    public override void ChaseMove(Enemy target)
    {
        float direction = target.transform.position.x - transform.position.x;
        Flip(direction);
        _chaseTime += Time.fixedDeltaTime;
        float fullRange = MaxAngle * 2f;
        float pingPongValue = Mathf.PingPong(_chaseTime * Enemy.Data.MoveSpeed * 50f, fullRange);
        float currentAngle = MaxAngle - pingPongValue;

        float rad = currentAngle * Mathf.Deg2Rad;
        float localX = Mathf.Sin(rad) * target.Radius;
        float localY = Mathf.Cos(rad) * target.Radius;

        Vector2 targetWorldPos = target.GetEnemyCenterPosition() + (Vector2.up * localY) + (Vector2.right * localX);
        if (!_isChasing)
        {
            Rigidbody.MovePosition(targetWorldPos);
            _isChasing = true;
            return;
        }

        var smoothPos = Vector2.Lerp(Rigidbody.position, targetWorldPos, _lerpValue * Time.fixedDeltaTime);

        Rigidbody.MovePosition(smoothPos);
    }

    public override void StopMove()
    {
        Rigidbody.linearVelocity = Vector2.zero;
    }

    private void Flip(float direction)
    {
        if (Mathf.Abs(direction) < 0.01f) return;

        Vector3 scale = transform.localScale;
        if (direction > 0f)
        {
            scale.x = Mathf.Abs(scale.x);
        }
        else if (direction < 0f)
        {
            scale.x = -Mathf.Abs(scale.x);
        }

        transform.localScale = scale;
    }
}
