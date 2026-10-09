using System.Collections;
using UnityEngine;

/// <summary>EP0 연습 공격. 실제 접촉 순간 기존 PlayerGuard 판정을 사용하고 체력 피해는 주지 않는다.</summary>
public sealed class Ep0TutorialMeleeCombat : EnemyCombat, IParryable
{
    [SerializeField] private Collider2D _attackCollider;
    [SerializeField] private string _attackName = "연습 공격";
    [SerializeField] private AttackDefense _defense = new AttackDefense { Parryable = true };
    [SerializeField, Min(0f)] private float _groggyDuration = 2f;

    private Coroutine _attackCoroutine;
    private bool _hitConsumed;
    public bool IsAttacking => _attackCoroutine != null;

    protected override void Awake()
    {
        base.Awake();
        _attackCollider.enabled = false;
    }

    public override void Attack()
    {
        if (_attackCoroutine != null) return;
        _hitConsumed = false;
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
        if (_hitConsumed || !_attackCollider.enabled || !_attackCollider.IsTouching(other)) return;
        Player player = other.GetComponentInParent<Player>();
        if (player == null || player.Health.IsDead) return;
        _hitConsumed = true;
        _attackCollider.enabled = false;
        var hitData = new HitData(
            Enemy.Data.MeleeAttackDamage,
            Enemy.Data.KnockbackForce,
            other.ClosestPoint(_attackCollider.bounds.center),
            Enemy.GetDirectionToPlayer(),
            _defense,
            this,
            attackName: _attackName
        );
        // 성공과 실패는 실제 입력으로 열린 가드/패링 시간에 따라 결정한다.
        player.Guard.TryHandleHit(hitData);
    }

    public void OnParried(HitData hitData, Transform parrier)
    {
        StopAttack();
        Enemy.Animation.PlayParriedHit();
        Enemy.EnterGroggy(_groggyDuration);
    }

    public override void StopAttack()
    {
        _hitConsumed = true;
        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
            _attackCoroutine = null;
        }
        _attackCollider.enabled = false;
    }

    private void OnDisable()
    {
        StopAttack();
    }
}
