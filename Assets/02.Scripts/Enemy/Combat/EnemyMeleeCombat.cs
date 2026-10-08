using System.Collections;
using UnityEngine;

public class EnemyMeleeCombat : EnemyCombat, IParryable
{
    [SerializeField] private Collider2D _attackCollider;
    [SerializeField] private string _attackName = "근접 공격";

    [Header("가드 / 패링")]

    [SerializeField] private AttackDefense _defense = new AttackDefense { Parryable = true };

    [Tooltip("패링당했을 때 그로기 시간 (초)")]
    [SerializeField, Min(0f)]
    private float _groggyDuration = 2f;

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
        // 본체 콜라이더로 들어온 트리거 이벤트는 무시하고 AttackZone에 닿은 경우만 처리
        if (!_attackCollider.enabled || !_attackCollider.IsTouching(other)) return;

        if (!other.TryGetComponent<IDamageable>(out IDamageable damageable)) return;
        // 적 본체 위치 대신 공격 판정 중심 기준으로 계산 (실제 맞은 높이에 가깝게)
        Vector2 hitPoint = other.ClosestPoint(_attackCollider.bounds.center);
        var hitData = new HitData(
            _currentDamage,
            Enemy.Data.KnockbackForce,
            hitPoint,
            Enemy.GetDirectionToPlayer(),
            _defense,
            this,
            _attackName
        );

        // 가드 / 패링으로 막히면 false
        if (damageable.TakeDamage(hitData))
        {
            _attackCollider.enabled = false;
            if (Enemy.MoveType is EnemyFlyingMove flyingMove)
            {
                flyingMove.ApplyHitSlowdown();
            }

            Debug.Log($"플레이어에게 근접공격으로 {_currentDamage}만큼 피해를 입힘.");
        }
    }

    public void OnParried(HitData hitData, Transform parrier)
    {
        // 패링당하면 공격 판정을 바로 끄고 그로기
        StopAttack();
        Enemy.EnterGroggy(_groggyDuration);
        Debug.Log($"{name}: 근접 공격이 패링당함 → 그로기 {_groggyDuration}초");
    }

    private void StopAttack()
    {
        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
            _attackCoroutine = null;
        }

        _attackCollider.enabled = false;
    }

    public void CalculateDamage(float percentage)
    {
        _currentDamage = Enemy.Data.MeleeAttackDamage * (1 + (percentage / 100f));
    }
}
