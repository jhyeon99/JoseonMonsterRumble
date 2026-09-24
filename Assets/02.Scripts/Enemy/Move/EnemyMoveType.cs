using UnityEngine;

public abstract class EnemyMoveType : MonoBehaviour
{
    protected Enemy Enemy { get; private set; }
    protected Rigidbody2D Rigidbody { get; private set; }

    protected virtual void Awake()
    {
        Enemy = GetComponent<Enemy>();
        Rigidbody = GetComponent<Rigidbody2D>();
    }

    public abstract void Move(Vector2 direction);
}