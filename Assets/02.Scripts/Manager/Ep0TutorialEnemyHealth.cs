using UnityEngine;

/// <summary>EP0 연습 몬스터의 피격만 보호한다. 일반 EnemyHealth는 수정하지 않는다.</summary>
public sealed class Ep0TutorialEnemyHealth : EnemyHealth, IDamageable
{
    bool IDamageable.TakeDamage(HitData hitData)
    {
        // 피격 이펙트와 실제 콤보 이벤트는 유지하고, 연습 중 사망과 넉백만 막는다.
        // EnemyHurtbox가 반환하는 IDamageable은 이 구현으로 연결된다.
        hitData.Damage = 0f;
        hitData.KnockbackForce = 0f;
        return base.TakeDamage(hitData);
    }
}
