using UnityEngine;

/// <summary>잡몹의 피격 영역과 기존 체력 컴포넌트를 연결한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CapsuleCollider2D))]
public sealed class EnemyHurtbox : MonoBehaviour
{
    [SerializeField] private EnemyHealth _health;

    private void Awake()
    {
        if (_health == null)
        {
            Debug.LogError("[EnemyHurtbox] 소유 EnemyHealth가 연결되지 않았습니다.", this);
        }

        CapsuleCollider2D hurtboxCollider = GetComponent<CapsuleCollider2D>();
        if (!hurtboxCollider.isTrigger)
        {
            Debug.LogError("[EnemyHurtbox] CapsuleCollider2D의 Is Trigger를 켜야 합니다.", this);
        }
    }

    /// <summary>잡몹은 Hurtbox만 허용하고, 다른 기존 피해 대상의 처리는 보존한다.</summary>
    public static bool TryGetDamageTarget(Collider2D hitCollider, out IDamageable damageable)
    {
        damageable = null;
        if (hitCollider == null || !hitCollider.isActiveAndEnabled)
        {
            return false;
        }

        if (hitCollider.TryGetComponent(out EnemyHurtbox hurtbox))
        {
            if (!hurtbox.isActiveAndEnabled || hurtbox._health == null ||
                !hurtbox._health.isActiveAndEnabled || !hurtbox._health.IsAlive)
            {
                return false;
            }

            damageable = hurtbox._health;
            return true;
        }

        if (hitCollider.GetComponentInParent<Enemy>() != null)
        {
            return false;
        }

        damageable = hitCollider.GetComponentInParent<IDamageable>();
        return damageable != null;
    }
}
