using System.Collections;
using UnityEngine;

public class EnemyRangedCombat : EnemyCombat
{
    [SerializeField] private Transform _muzzlePoint;
    [SerializeField] private ProjectileType _type;

    [Header("특수 발사 설정")]
    [SerializeField, Range(0f, 1f)] private float _burstChance = 0.15f;
    [SerializeField, Range(0f, 1f)] private float _spreadChance = 0.15f;
    [SerializeField, Min(0f)] private float _burstMinDistance = 6.5f;
    [SerializeField] private float _burstInterval = 0.28f;
    [SerializeField] private float _spreadAngle = 15f;

    public event System.Action OnTelegraphAttack;

    public void OnTelegraph()
    {
        OnTelegraphAttack?.Invoke();
    }

    public override void Attack()
    {
        if (Enemy == null || Enemy.Target == null || _muzzlePoint == null)
        {
            return;
        }

        if (Enemy.MoveType is EnemyFlyingMove && (_burstChance > 0f || _spreadChance > 0f))
        {
            float roll = Random.value;
            float distanceToPlayer = Enemy.GetDistanceToPlayer();

            if (roll < _burstChance && distanceToPlayer >= _burstMinDistance)
            {
                StartCoroutine(FireBurstRoutine());
                return;
            }

            if (roll < _burstChance + _spreadChance)
            {
                FireSpreadProjectiles();
                return;
            }
        }

        FireSingleProjectile(0f);
    }

    private void FireSingleProjectile(float angleOffset = 0f)
    {
        if (Enemy == null || Enemy.Target == null || _muzzlePoint == null)
        {
            return;
        }

        Projectile projectile =
            ProjectilePool.Instance.GetProjectile(_type, _muzzlePoint, Enemy.Target, DamageIncreaseValue);
        if (projectile == null)
        {
            return;
        }

        projectile.transform.position = _muzzlePoint.position;
        projectile.transform.rotation = _muzzlePoint.rotation;

        Vector2 direction = Enemy.GetDirectionToPlayer();
        float baseCorrection = direction.x <= 0f ? 8.228f : -8.228f;
        projectile.transform.Rotate(0f, 0f, baseCorrection + angleOffset);
    }

    private void FireSpreadProjectiles()
    {
        FireSingleProjectile(0f);
        FireSingleProjectile(_spreadAngle);
        FireSingleProjectile(-_spreadAngle);
    }

    private IEnumerator FireBurstRoutine()
    {
        FireSingleProjectile(0f);
        yield return new WaitForSeconds(_burstInterval);
        FireSingleProjectile(0f);
    }
}
