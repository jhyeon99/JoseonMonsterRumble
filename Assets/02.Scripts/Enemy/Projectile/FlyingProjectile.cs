using UnityEngine;

public class FlyingProjectile : Projectile
{
    // 로컬 왼쪽으로 직진하므로 실제 진행 방향 = -transform.right
    protected override Vector2 HitDirection => -transform.right;

    protected override void Fire()
    {
        Vector2 direction = Vector2.left;
        transform.Translate(direction * Speed * Time.deltaTime);
    }
}
