using UnityEngine;


public class EnemyFlipFlying : EnemyFlip

{
    public override void Flip()

    {
        if (Enemy.Combat is EnemyMeleeCombat melee && melee.FlyingAttack != null)
        {
            melee.FlyingAttack.FaceDirection(Enemy.GetDirectionToPlayer());
            return;
        }
        if (Enemy.Combat is EnemyRangedCombat)
        {
            // 원거리 비행은 EnemyRotation이 회전과 뒤집기를 함께 처리한다.
            return;
        }
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
