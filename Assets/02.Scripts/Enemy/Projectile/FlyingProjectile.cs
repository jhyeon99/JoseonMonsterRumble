using UnityEngine;

public class FlyingProjectile : Projectile
{
    protected override void Fire()
    {
        Vector2 direction = Vector2.left;
        transform.Translate(direction * Speed * Time.deltaTime);
    }
}
