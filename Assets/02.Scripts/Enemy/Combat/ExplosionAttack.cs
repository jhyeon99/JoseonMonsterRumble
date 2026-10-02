using System;
using UnityEngine;

public class ExplosionAttack : MonoBehaviour
{
    private Enemy _enemy;
    private float _currentDamage;

    private void Start()
    {
        _enemy = GetComponentInParent<Enemy>();
        _currentDamage = _enemy.Data.MeleeAttackDamage;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent<IDamageable>(out IDamageable damageable)) return;
        Vector2 hitPoint = other.ClosestPoint(transform.position);
        var hitData = new HitData(
            _currentDamage,
            _enemy.Data.KnockbackForce,
            hitPoint,
            _enemy.GetDirectionToPlayer()
        );

        damageable.TakeDamage(hitData);
        _enemy.gameObject.SetActive(false);
        Debug.Log($"플레이어에게 폭파공격으로 {_currentDamage}만큼 피해를 입힘.");
    }
}
