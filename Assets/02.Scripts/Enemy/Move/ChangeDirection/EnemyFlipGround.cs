using UnityEngine;

public class EnemyFlipGround : EnemyFlip
{
    protected override void Flip()
    {
        if (Mathf.Abs(Enemy.GetDirectionToPlayer().x) < 0.01f) return;

        Vector3 scale = transform.localScale;
        if (!Enemy.MoveType.IsBackMove)
        {
            if (Enemy.GetDirectionToPlayer().x > 0f)
            {
                scale.x = Mathf.Abs(scale.x);
            }
            else if (Enemy.GetDirectionToPlayer().x < 0f)
            {
                scale.x = -Mathf.Abs(scale.x);
            }
        }
        else
        {
            if (Enemy.GetDirectionToPlayer().x > 0f)
            {
                scale.x = -Mathf.Abs(scale.x);
            }
            else if (Enemy.GetDirectionToPlayer().x < 0f)
            {
                scale.x = Mathf.Abs(scale.x);
            }
        }

        transform.localScale = scale;
    }
}