using UnityEngine;

public abstract class EnemyFlip : MonoBehaviour
{
    protected Enemy Enemy { get; private set; }

    private void Awake()
    {
        Enemy = GetComponent<Enemy>();
    }

    private void Update()
    {
        Flip();
    }

    public abstract void Flip();
}
