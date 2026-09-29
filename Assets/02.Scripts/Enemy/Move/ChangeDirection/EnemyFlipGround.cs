using UnityEngine;

public class EnemyFlipGround : EnemyFlip
{
    protected override void Flip()
    {
        transform.localScale = Enemy.GetDirectionToPlayer().x > 0 ? new Vector3(-1f, 1f, 1f) : new Vector3(1f, 1f, 1f);
    }
}