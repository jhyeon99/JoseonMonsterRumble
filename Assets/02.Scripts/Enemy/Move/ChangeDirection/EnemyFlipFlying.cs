using UnityEngine;

public class EnemyFlipFlying : EnemyFlip
{
    protected override void Flip()
    {
        Vector3 scale = transform.localScale;
        if (Enemy.GetDirectionToPlayer().x >= 0f)
        {
            scale.y = -Mathf.Abs(scale.y);
        }
        else
        {
            scale.y = Mathf.Abs(scale.y);
        }

        transform.localScale = scale;
    }
}
