using System.Collections;
using UnityEngine;

public class EnemyRangedCombat : EnemyCombat
{
    [SerializeField] private Transform _muzzlePoint;
    [SerializeField] private ProjectileType _type;

    public override void Attack()
    {
        Projectile projectile =
            ProjectilePool.Instance.GetProjectile(_type, _muzzlePoint, Enemy.Target, DamageIncreaseValue);
        projectile.transform.position = _muzzlePoint.position;
        projectile.transform.rotation = _muzzlePoint.rotation;
        Vector2 direction = Enemy.GetDirectionToPlayer();
        if (direction.x <= 0f)
        {
            projectile.transform.Rotate(0f, 0f, 10.5f);
        }
        else
        {
            projectile.transform.Rotate(0f, 0f, -10.5f);
        }
    }
}
