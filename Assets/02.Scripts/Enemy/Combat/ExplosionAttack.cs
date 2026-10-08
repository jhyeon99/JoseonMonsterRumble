using System;
using UnityEngine;

public class ExplosionAttack : MonoBehaviour
{
    [SerializeField] private string _attackName = "폭발 공격";

    private Enemy _enemy;
    private Collider2D _explosionCollider;
    private float _currentDamage;

    private void Start()
    {
        _enemy = GetComponentInParent<Enemy>();
        _explosionCollider = GetComponent<Collider2D>();
        _currentDamage = _enemy.Data.MeleeAttackDamage;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent<IDamageable>(out IDamageable damageable)) return;
        // 오브젝트 위치 대신 폭발 판정 중심 기준으로 계산 (실제 맞은 높이에 가깝게)
        Vector2 origin = _explosionCollider != null ? (Vector2)_explosionCollider.bounds.center : (Vector2)transform.position;
        Vector2 hitPoint = other.ClosestPoint(origin);
        var hitData = new HitData(
            _currentDamage,
            _enemy.Data.KnockbackForce,
            hitPoint,
            _enemy.GetDirectionToPlayer(),
            default,
            null,
            _attackName
        );

        damageable.TakeDamage(hitData);
        gameObject.SetActive(false);
        Debug.Log($"플레이어에게 폭파공격으로 {_currentDamage}만큼 피해를 입힘.");
        _enemy.StateMachine.ChangeState(new DeadState(_enemy));
    }
}