using System;
using System.Collections;
using UnityEngine;

/// <summary>EP0 전용 공격. 실제 접촉을 보류한 뒤 기존 PlayerGuard로 패링을 판정한다.</summary>
public sealed class Ep0TutorialMeleeCombat : EnemyCombat, IParryable
{
    [SerializeField] private Collider2D _attackCollider;
    [SerializeField] private string _attackName = "연습 공격";
    [SerializeField] private AttackDefense _defense = new AttackDefense { Parryable = true };
    [SerializeField, Min(0f)] private float _groggyDuration = 2f;

    private Coroutine _attackCoroutine;
    private bool _deferNextPlayerHit;
    private PlayerGuard _deferredGuard;
    private HitData _deferredHit;

    public event Action OnHitDeferred;

    protected override void Awake()
    {
        base.Awake();
        _attackCollider.enabled = false;
    }

    public void DeferNextPlayerHit()
    {
        _deferredGuard = null;
        _deferNextPlayerHit = true;
    }

    public override void Attack()
    {
        if (_attackCoroutine == null) _attackCoroutine = StartCoroutine(AttackRoutine());
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
        if (!_deferNextPlayerHit || !_attackCollider.enabled || !_attackCollider.IsTouching(other)) return;
        Player player = other.GetComponentInParent<Player>();
        if (player == null || player.Health.IsDead) return;

        _deferNextPlayerHit = false;
        _deferredGuard = player.Guard;
        _deferredHit = new HitData(
            Enemy.Data.MeleeAttackDamage,
            Enemy.Data.KnockbackForce,
            other.ClosestPoint(_attackCollider.bounds.center),
            Enemy.GetDirectionToPlayer(),
            _defense,
            this,
            attackName: _attackName
        );
        // 보류 중 추가 접촉은 받지 않으며, 연습 공격은 플레이어 체력을 깎지 않는다.
        _attackCollider.enabled = false;
        OnHitDeferred?.Invoke();
    }

    public void ResolveDeferredHit()
    {
        PlayerGuard guard = _deferredGuard;
        HitData hitData = _deferredHit;
        _deferredGuard = null;
        _deferNextPlayerHit = false;
        if (guard != null) guard.TryHandleHit(hitData);
    }

    public void OnParried(HitData hitData, Transform parrier)
    {
        StopAttack();
        Enemy.Animation.PlayParriedHit();
        Enemy.EnterGroggy(_groggyDuration);
    }

    public override void StopAttack()
    {
        _deferNextPlayerHit = false;
        _deferredGuard = null;
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
