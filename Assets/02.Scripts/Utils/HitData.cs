using UnityEngine;

public struct HitData
{
    public float Damage;
    public float KnockbackForce;
    public Vector2 HitPoint;
    public Vector2 AttackDirection;

    public HitData(float damage, float knockbackForce, Vector2 hitPoint, Vector2 attackDirection)
    {
        Damage = damage;
        KnockbackForce = knockbackForce;
        HitPoint = hitPoint;
        AttackDirection = attackDirection;
    }
}