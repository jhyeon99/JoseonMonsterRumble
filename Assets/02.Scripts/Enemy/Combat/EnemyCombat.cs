using UnityEngine;

public abstract class EnemyCombat : MonoBehaviour
{
    protected Enemy Enemy { get; private set; }
    protected float DamageIncreaseValue { get; private set; }

    protected virtual void Awake()
    {
        Enemy = GetComponent<Enemy>();
    }

    public abstract void Attack();

    public void IncreaseDamge(float increaseValue)
    {
        DamageIncreaseValue = increaseValue;
    }

    public void NormalDamage()
    {
        DamageIncreaseValue = 0f;
    }
}
