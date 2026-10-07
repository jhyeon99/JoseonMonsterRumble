using UnityEngine;


public class EnemyFlipFlying : EnemyFlip

{
    protected override void Flip()

    {
        Vector3 scale = transform.localScale;

        if (Enemy.GetDirectionToPlayer().x >= 0f)

        {
            scale.x = -1f;
            scale.y = -1f;
        }

        else

        {
            scale.x = -1f;
            scale.y = 1f;
        }

        transform.localScale = scale;
    }
}
