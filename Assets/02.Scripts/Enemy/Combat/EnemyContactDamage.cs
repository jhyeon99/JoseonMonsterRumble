using UnityEngine;

/// <summary>
/// 적 몸통 접촉 데미지. 적의 자식 오브젝트(예: ContactZone)에 붙이고,
/// 같은 오브젝트에 Is Trigger를 켠 Collider2D를 둔다.
/// 레이어를 EnemyAttack으로 두면 플레이어 무적(회피 등) 중에는 충돌 자체가 꺼져서 자연스럽게 통과됨.
/// 보스 자식에 붙여도 동작 (보스가 대기 / 패턴 중일 때만 데미지, 등장 / 그로기 / 하울링 / 처치 중에는 없음).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class EnemyContactDamage : MonoBehaviour
{
    [Header("접촉 데미지")]

    [SerializeField] private string _attackName = "접촉 피해";
    [SerializeField, Min(0f)] private float _damage = 10f;

    [Tooltip("0이면 넉백 없이 제자리 경직")]
    [SerializeField, Min(0f)]
    private float _knockbackForce = 5f;

    [Tooltip("겹쳐 있는 동안 판정 간격 (초). 가드로 막는 중에 매 프레임 게이지가 깎이지 않도록")]
    [SerializeField, Min(0f)]
    private float _hitInterval = 0.5f;

    [Header("가드 / 패링")]

    [Tooltip("기본은 패링 불가, 가드 가능")]
    [SerializeField]
    private AttackDefense _defense;

    private float _nextHitTime;

    private Enemy _enemy;
    private Boss _boss;
    private Collider2D _zone;

    private void Awake()
    {
        _enemy = GetComponentInParent<Enemy>();
        _boss = _enemy == null ? GetComponentInParent<Boss>() : null;
        _zone = GetComponent<Collider2D>();

        if (_enemy == null && _boss == null)
        {
            Debug.LogWarning($"{name}: 부모에서 Enemy 또는 Boss를 찾지 못했습니다.", this);
            enabled = false;
            return;
        }

        if (!_zone.isTrigger)
        {
            Debug.LogWarning($"{name}: Collider2D의 Is Trigger가 꺼져 있습니다.", this);
        }
    }

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    // Enter: 처음 닿은 순간 (Rigidbody가 잠들어 Stay가 안 오는 경우 대비)
    private void OnTriggerEnter2D(Collider2D other) => TryDamage(other);

    // Stay: 무적이 끝났는데 계속 겹쳐 있으면 다시 맞도록 (무적 중에는 TakeDamage가 무시됨)
    private void OnTriggerStay2D(Collider2D other) => TryDamage(other);

    private void TryDamage(Collider2D other)
    {
        if (!enabled) return;
        if (other.gameObject.layer != Constants.Layers.Player) return;

        // 죽는 중 / 날아가는 중 / 그로기 중인 적은 접촉 데미지 없음 (그로기 적 앞에서 마무리 공격을 해야 하므로)
        if (_enemy != null && (_enemy.IsDefeated || _enemy.IsGroggy)) return;

        // 보스는 대기 / 패턴 중일 때만 (등장, 그로기, 하울링, 완전 그로기, 처치 중에는 없음)
        if (_boss != null && !_boss.CanDealContactDamage) return;

        if (Time.time < _nextHitTime) return;

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable == null) return;

        // 적 → 플레이어 방향으로 밀어냄 (PlayerHitState는 x 부호만 사용)
        Vector2 attackDirection = other.bounds.center.x >= _zone.bounds.center.x
            ? Vector2.right
            : Vector2.left;

        Vector2 hitPoint = other.ClosestPoint(_zone.bounds.center);

        var hitData = new HitData(_damage, _knockbackForce, hitPoint, attackDirection, _defense, null, 0, _attackName);
        damageable.TakeDamage(hitData);
        _nextHitTime = Time.time + _hitInterval;
    }
}
