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
    /// <summary>공격 명칭 / 사망 원인</summary>
    public string AttackName;

    /// <summary>플레이어가 패링하여 되돌려 보낸 적 투사체인지</summary>
    public bool IsReflectedProjectile;

    public bool IsTalisman;

    /// <summary>공격한 대상 (적 / 보스 등). 사망 원인 표시용. 모르면 null</summary>
    public GameObject Attacker;

    /// <summary>공격한 대상의 표시 이름 (보스는 보스 이름, 그 외는 오브젝트 이름). 모르면 빈 문자열</summary>
    public string AttackerName => GetDisplayName(Attacker);

    public bool IsHeavyImpact => ComboStep == HeavyComboStep || IsReflectedProjectile;

    public HitData(
        float damage,
        float knockbackForce,
        Vector2 hitPoint,
        Vector2 attackDirection,
        AttackDefense defense = default,
        IParryable source = null,
        int comboStep = 0,
        string attackName = "",
        bool isReflectedProjectile = false,
        bool isTalisman = false
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
        IsTalisman = isTalisman;
        AttackName = attackName;
        Attacker = null;
    }

    /// <summary>오브젝트의 표시 이름. 보스면 보스 이름, 그 외는 (Clone) / PF_ 를 뗀 오브젝트 이름</summary>
    public static string GetDisplayName(GameObject target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        Boss boss = target.GetComponentInParent<Boss>();
        if (boss != null && !string.IsNullOrWhiteSpace(boss.BossName))
        {
            return boss.BossName;
        }

        string name = target.name.Replace("(Clone)", string.Empty).Trim();
        return name.StartsWith("PF_") ? name.Substring(3) : name;
    }
}
