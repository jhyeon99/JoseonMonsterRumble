using UnityEngine;

public abstract class EnemyMoveType : MonoBehaviour
{
    protected Enemy Enemy { get; private set; }
    protected Rigidbody2D Rigidbody { get; private set; }
    [HideInInspector] public bool IsBackMove = false;

    protected virtual void Awake()
    {
        Enemy = GetComponent<Enemy>();
        Rigidbody = GetComponent<Rigidbody2D>();
    }

    public abstract void Move(Vector2 direction);
    public abstract void BackMove(Vector2 direction);
    public abstract void StopMove();

    public virtual void ChaseMove(Transform target)
    {
    }

    public virtual void MoveToStartPosition(Transform target)
    {
    }

    public virtual void PatrolIdleMove()
    {
    }

    public virtual void MoveToInitialPosition()
    {
    }

    public bool IsArrivedAtStart()
    {
        Vector2 startPos = Enemy.MoveArea.StartPosition;
        return Vector2.Distance(Rigidbody.position, startPos) < 0.1f;
    }

    public void ChargeMove(Vector2 direction)
    {
        Rigidbody.linearVelocity = Vector2.zero;
        Rigidbody.linearVelocity = direction * Enemy.Data.ChargeSpeed;
    }
}