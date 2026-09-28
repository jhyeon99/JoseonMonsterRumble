using System.Collections;
using UnityEngine;

public class EnemyRangedCombat : EnemyCombat
{
    [SerializeField] private Transform _muzzlePoint;
    [SerializeField] private ProjectileType _type;

    public override void Attack()
    {
        Projectile projectile = ProjectilePool.Instance.GetProjectile(_type, _muzzlePoint);
        projectile.transform.position = _muzzlePoint.position;
        Vector3 direction = Enemy.GetDirectionToPlayer();
        float angle = Vector2.SignedAngle(Vector2.right, direction);
        projectile.transform.rotation = Quaternion.Euler(0f, 0f, angle - 180f);
    }
}
