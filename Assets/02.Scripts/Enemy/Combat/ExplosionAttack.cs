using UnityEngine;

public class ExplosionAttack : MonoBehaviour
{
    private const int MaxOverlapCount = 8;
    [SerializeField] private string _attackName = "폭발 공격";
    [SerializeField] private AttackDefense _attackDefense;

    private Enemy _enemy;
    private Collider2D _explosionCollider;
    private readonly Collider2D[] _overlaps = new Collider2D[MaxOverlapCount];
    private ContactFilter2D _playerFilter;

    private void Awake()
    {
        _enemy = GetComponentInParent<Enemy>();
        _explosionCollider = GetComponent<Collider2D>();
        _playerFilter.SetLayerMask(1 << Constants.Layers.Player);
        _playerFilter.useTriggers = true;
    }

    public void Explode()
    {
        // 접촉 콜백을 기다리지 않고 폭발 순간의 범위와 회피 무적을 한 번만 판정한다.
        _explosionCollider.enabled = true;
        int count = _explosionCollider.Overlap(_playerFilter, _overlaps);
        Vector2 origin = _explosionCollider.bounds.center;
        for (int i = 0; i < count; i++)
        {
            Collider2D other = _overlaps[i];
            IDamageable damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null)
            {
                continue;
            }

            var hitData = new HitData(
                _enemy.Data.MeleeAttackDamage,
                _enemy.Data.KnockbackForce,
                other.ClosestPoint(origin),
                _enemy.GetDirectionToPlayer(),
                _attackDefense,
                null,
                0,
                _attackName
            );
            hitData.Attacker = _enemy.gameObject;
            damageable.TakeDamage(hitData);
            // 단일 플레이어의 여러 콜라이더에 중복 피해를 주지 않는다.
            break;
        }
        _explosionCollider.enabled = false;
    }
}
