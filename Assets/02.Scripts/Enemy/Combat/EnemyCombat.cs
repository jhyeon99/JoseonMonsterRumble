using UnityEngine;

public abstract class EnemyCombat : MonoBehaviour
{
    protected Enemy Enemy { get; private set; }

    protected virtual void Awake()
    {
        Enemy = GetComponent<Enemy>();
    }

    public abstract void Attack();
}
