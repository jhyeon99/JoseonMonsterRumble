using System.Collections;
using UnityEngine;

public class EnemyMeleeCombat : EnemyCombat
{
    [SerializeField] private Collider2D _attackCollider;
    private Coroutine _attackCoroutine;
    private float _currentDamage;
    public float CurrentDamage => _currentDamage;

    protected override void Awake()
    {
        base.Awake();
        _attackCollider.enabled = false;
    }

    public override void Attack()
    {
        if (_attackCoroutine != null) return;
        CalculateDamage(DamageIncreaseValue);

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
            _currentDamage,
            0f,
            Vector2.zero,
            Vector2.zero
        );

        damageable.TakeDamage(hitData);
        Debug.Log($"플레이어에게 근접공격으로 {_currentDamage}만큼 피해를 입힘.");
    }

    public void CalculateDamage(float percentage)
    {
        _currentDamage = Enemy.Data.MeleeAttackDamage * (1 + (percentage / 100f));
    }
}
