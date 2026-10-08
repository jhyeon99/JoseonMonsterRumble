using UnityEngine;

public struct HitData
{
    public float Damage;
    public float KnockbackForce;
    public Vector2 HitPoint;
    public Vector2 AttackDirection;

    /// <summary>가드 / 패링 규칙. 기본값은 패링 불가, 가드 가능</summary>
    public AttackDefense Defense;

    /// <summary>패링당했을 때 반응할 대상 (그로기, 반사 등). 반응이 없으면 null</summary>
    public IParryable Source;

    public HitData(
        float damage,
        float knockbackForce,
        Vector2 hitPoint,
        Vector2 attackDirection,
        AttackDefense defense = default,
        IParryable source = null
    )
    {
        Damage = damage;
        KnockbackForce = knockbackForce;
        HitPoint = hitPoint;
        AttackDirection = attackDirection;
        Defense = defense;
        Source = source;
    }
}
