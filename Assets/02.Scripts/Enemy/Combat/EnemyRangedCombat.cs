using System.Collections;
using UnityEngine;

public class EnemyRangedCombat : EnemyCombat
{
    [SerializeField] private Transform _muzzlePoint;
    [SerializeField] private ProjectileType _type;

    public override void Attack()
    {
        Projectile projectile = ProjectilePool.Instance.GetProjectile(_type, _muzzlePoint, Enemy.Target);
        projectile.transform.position = _muzzlePoint.position;
        projectile.transform.rotation = _muzzlePoint.rotation;
    }
}
