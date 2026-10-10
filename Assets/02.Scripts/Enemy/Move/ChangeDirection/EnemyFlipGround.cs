using UnityEngine;

public class EnemyFlipGround : EnemyFlip
{
    public override void Flip()
    {
        // 걷기 중 방향 전환은 감속 종료 후 이동 코드가 처리한다. 공격 방향 잠금은 기존 경로를 유지한다.
        if (Enemy.MoveType is EnemyGroundMove groundMove && groundMove.HasWalkFacingControl)
        {
            return;
        }

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
