using UnityEngine;

public class EnemyRangedCombat : EnemyCombat
{
    private const float LaunchAngleOffset = 8.228f;

    [SerializeField] private Transform _muzzlePoint;
    [SerializeField] private ProjectileType _type;

    public override void Attack()
    {
        ProjectilePool pool = ProjectilePool.Instance;
        if (pool == null || _muzzlePoint == null || Enemy.Target == null)
        {
            Debug.LogWarning("EnemyRangedCombat: 투사체 풀·총구·플레이어가 없어 발사를 건너뜁니다.", this);
            return;
        }

        Projectile projectile =
            pool.GetProjectile(_type, _muzzlePoint, Enemy.Target, DamageIncreaseValue);
        if (projectile == null)
        {
            Debug.LogWarning("EnemyRangedCombat: 해당 유형의 투사체가 없거나 모두 사용 중이므로 발사를 건너뜁니다.", this);
            return;
        }

        projectile.transform.position = _muzzlePoint.position;
        projectile.transform.rotation = _muzzlePoint.rotation;
        Vector2 direction = Enemy.GetDirectionToPlayer();
        if (direction.x <= 0f)
        {
            projectile.transform.Rotate(0f, 0f, LaunchAngleOffset);
        }
        else
        {
            projectile.transform.Rotate(0f, 0f, -LaunchAngleOffset);
        }
    }
}
