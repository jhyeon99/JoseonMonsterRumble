using UnityEngine;


public class EnemyFlipFlying : EnemyFlip

{
    public override void Flip()

    {
        Vector3 scale = transform.localScale;

        if (Enemy.GetDirectionToPlayer().x >= 0f)
        {
            scale.x = -Mathf.Abs(scale.x);
            scale.y = -Mathf.Abs(scale.y);
        }
        else
        {
            scale.x = -Mathf.Abs(scale.x);
            scale.y = Mathf.Abs(scale.y);
        }

        transform.localScale = scale;
    }
}
