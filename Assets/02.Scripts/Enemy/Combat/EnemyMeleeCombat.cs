using System.Collections;
using UnityEngine;

public class EnemyMeleeCombat : EnemyCombat
{
    [SerializeField] private Collider2D _attackCollider;
    private Coroutine _attackCoroutine;

    protected override void Awake()
    {
        base.Awake();
        _attackCollider.enabled = false;
    }

    public override void Attack()
    {
        if (_attackCoroutine != null) return;

        _attackCoroutine = StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        _attackCollider.enabled = true;

        yield return new WaitForSeconds(Enemy.Data.AttackDurationTime);

        _attackCollider.enabled = false;
        _attackCoroutine = null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent<IDamageable>(out IDamageable damageable)) return;

        var hitData = new HitData(
            Enemy.Data.AttackDamage,
            0f,
            Vector2.zero,
            Enemy.GetDirectionToPlayer()
        );

        damageable.TakeDamage(hitData);
        Debug.Log($"플레이어에게{Enemy.Data.AttackDamage}만큼 피해를 입힘.");
        Debug.Log($"플레이어에게 {Enemy.GetDirectionToPlayer()}방향으로 넉백.");
    }
}