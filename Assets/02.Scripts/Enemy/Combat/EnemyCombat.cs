using UnityEngine;

public abstract class EnemyCombat : MonoBehaviour
{
    protected Enemy Enemy { get; private set; }
    protected float DamageIncreaseValue { get; private set; }
    public float ExtraGuardGaugeCost { get; private set; }

    protected virtual void Awake()
    {
        Enemy = GetComponent<Enemy>();
    }

    public abstract void Attack();

    public virtual void StopAttack() { }

    public void IncreaseDamage(float increaseValue)
    {
        DamageIncreaseValue = increaseValue;
    }

    public void NormalDamage()
    {
        DamageIncreaseValue = 0f;
    }

    public void SetExtraGuardGaugeCost(float extraCost)
    {
        ExtraGuardGaugeCost = extraCost;
    }

    public void ResetExtraGuardGaugeCost()
    {
        ExtraGuardGaugeCost = 0f;
    }

    public virtual AttackDefense GetEffectiveDefense(AttackDefense baseDefense)
    {
        if (ExtraGuardGaugeCost <= 0f)
        {
            return baseDefense;
        }

        return new AttackDefense
        {
            Parryable = baseDefense.Parryable,
            GuardGaugeCost = baseDefense.GuardGaugeCost + ExtraGuardGaugeCost
        };
    }
}
