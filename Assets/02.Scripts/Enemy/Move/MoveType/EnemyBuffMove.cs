using UnityEngine;

public class EnemyBuffMove : EnemyMoveType
{
    [SerializeField] private float _radius = 1f;
    [SerializeField] private float _lerpValue = 5f;
    [Range(0f, 180f)] public float MaxAngle = 60f;

    public override void Move(Vector2 direction)
    {
        Rigidbody.linearVelocity = direction * Enemy.Data.MoveSpeed;
    }

    public override void BackMove(Vector2 direction)
    {
    }

    private Vector2 GetChaseStartPosition(Transform target)
    {
        float startAngle = MaxAngle;
        float rad = startAngle * Mathf.Deg2Rad;

        float localX = Mathf.Sin(rad) * _radius;
        float localY = Mathf.Cos(rad) * _radius;

        return (Vector2)target.position + (Vector2.up * localY) + (Vector2.right * localX);
    }

    public override void MoveToStartPosition(Transform target)
    {
        Vector2 startWorldPos = GetChaseStartPosition(target);

        var smoothPos = Vector2.Lerp(Rigidbody.position, startWorldPos, _lerpValue * Time.fixedDeltaTime);
        Rigidbody.MovePosition(smoothPos);
        if (Vector2.Distance(Rigidbody.position, startWorldPos) < 0.1f)
        {
            Enemy.StateMachine.ChangeState(new BuffState(Enemy, target));
        }
    }

    public override void ChaseMove(Transform target)
    {
        float fullRange = MaxAngle * 2f;
        float pingPongValue = Mathf.PingPong(Time.fixedTime * Enemy.Data.MoveSpeed * 50f, fullRange);
        float currentAngle = pingPongValue - MaxAngle;

        float rad = currentAngle * Mathf.Deg2Rad;
        float localX = Mathf.Sin(rad) * _radius;
        float localY = Mathf.Cos(rad) * _radius;

        Vector2 targetWorldPos = (Vector2)target.position + (Vector2.up * localY) + (Vector2.right * localX);
        var smoothPos = Vector2.Lerp(Rigidbody.position, targetWorldPos, _lerpValue * Time.fixedDeltaTime);

        Rigidbody.MovePosition(smoothPos);
    }

    public override void StopMove()
    {
        Rigidbody.linearVelocity = Vector2.zero;
    }
}
