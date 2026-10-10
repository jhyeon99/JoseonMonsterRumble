using UnityEngine;

public class EnemyBuffCombat : EnemyCombat
{
    [SerializeField] private Collider2D _attackCollider;
    private ExplosionAttack _explosionAttack;

    protected override void Awake()
    {
        base.Awake();
        if (_attackCollider != null)
        {
            _attackCollider.enabled = false;
            _explosionAttack = _attackCollider.GetComponent<ExplosionAttack>();
        }
        else
        {
            _explosionAttack = GetComponentInChildren<ExplosionAttack>();
        }

        if (_explosionAttack == null)
        {
            Debug.LogError($"{name}: 자폭 판정에 ExplosionAttack이 없습니다.", this);
        }
    }

    public override void Attack()
    {
        if (_explosionAttack != null)
        {
            _explosionAttack.Explode();
        }
    }
}
