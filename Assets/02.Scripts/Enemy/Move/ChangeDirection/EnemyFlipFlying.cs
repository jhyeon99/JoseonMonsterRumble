using UnityEngine;


public class EnemyFlipFlying : EnemyFlip

{
    private EnemyRotation _rotation;

    private void Start()
    {
        _rotation = GetComponent<EnemyRotation>();
    }

    public override void Flip()

    {
        if (Enemy.Combat is EnemyMeleeCombat melee && melee.FlyingAttack != null)
        {
            melee.FlyingAttack.FaceDirection(Enemy.GetDirectionToPlayer());
            return;
        }
        if (Enemy.Combat is EnemyRangedCombat && _rotation != null)
        {
            _rotation.FaceDirection(Enemy.GetDirectionToPlayer());
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
