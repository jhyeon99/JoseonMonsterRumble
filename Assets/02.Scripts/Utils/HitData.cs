using UnityEngine;

public struct HitData
{
    private const int HeavyComboStep = 3;

    public float Damage;
    public float KnockbackForce;
    public Vector2 HitPoint;
    public Vector2 AttackDirection;

    /// <summary>가드 / 패링 규칙. 기본값은 패링 불가, 가드 가능</summary>
    public AttackDefense Defense;

    /// <summary>패링당했을 때 반응할 대상 (그로기, 반사 등). 반응이 없으면 null</summary>
    public IParryable Source;

    /// <summary>플레이어 근접 콤보 단계 (1 = 1타, 2 = 2타, 3 = 3타). 콤보 공격이 아니면 0</summary>
    public int ComboStep;

    /// <summary>플레이어가 패링하여 되돌려 보낸 적 투사체인지</summary>
    public bool IsReflectedProjectile;

    public bool IsHeavyImpact => ComboStep == HeavyComboStep || IsReflectedProjectile;

    public HitData(
        float damage,
        float knockbackForce,
        Vector2 hitPoint,
        Vector2 attackDirection,
        AttackDefense defense = default,
        IParryable source = null,
        int comboStep = 0,
        bool isReflectedProjectile = false
    )
    {
        Damage = damage;
        KnockbackForce = knockbackForce;
        HitPoint = hitPoint;
        AttackDirection = attackDirection;
        Defense = defense;
        Source = source;
        ComboStep = comboStep;
        IsReflectedProjectile = isReflectedProjectile;
    }
}
