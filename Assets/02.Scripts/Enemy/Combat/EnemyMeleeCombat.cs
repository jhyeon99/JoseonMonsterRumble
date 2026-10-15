using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class EnemyMeleeCombat : EnemyCombat, IParryable
{
    [SerializeField] private Collider2D _attackCollider;
    [SerializeField] private string _attackName = "근접 공격";

    [Header("가드 / 패링")]

    [SerializeField] private AttackDefense _defense = new AttackDefense { Parryable = true };

    [Tooltip("패링당했을 때 그로기 시간 (초)")]
    [SerializeField, Min(0f)]
    private float _groggyDuration = 2f;

    [Header("후속 판정 방지")]
    [Tooltip("공격 한 번에 같은 대상은 한 번만 판정. 맞음 / 가드 / 패링 / 회피(무적 중 통과) 중 하나로 처리되면 " +
             "같은 공격이 끝날 때까지 다시 판정하지 않음 (회피 무적이 끝나는 순간 판정 안에 있어도 맞지 않음)")]
    [SerializeField]
    private bool _oneResolvePerAttack = true;

    [Tooltip("공격이 대상에게 처리된 뒤(맞음 / 가드 / 회피) 이 시간 동안 몸통 접촉 데미지를 주지 않음 (초). " +
             "가드를 풀거나 회피가 끝난 직후 몸통에 바로 맞지 않도록")]
    [SerializeField, Min(0f)]
    private float _contactDamageGraceAfterAttack = 0.5f;

    private readonly HashSet<IDamageable> _resolvedTargets = new HashSet<IDamageable>();
    private readonly List<Collider2D> _overlapResults = new List<Collider2D>();
    private ContactFilter2D _playerFilter;
    private bool _hasResolvedTarget;
    private float _contactGraceUntil;
    private Coroutine _attackCoroutine;
    private float _currentDamage;
    private Transform _heightTarget;
    private Collider2D _heightTargetCollider;
    private BoxCollider2D _groundAttackBox;
    private FlyingMeleeAttackController _flyingAttack;
    private UnityEngine.Object _tutorialParrySuppressionOwner;
    public float CurrentDamage => _currentDamage;
    public FlyingMeleeAttackController FlyingAttack => _flyingAttack;
    public event Action OnAttackHit;

    public void SetTutorialParrySuppressed(UnityEngine.Object owner, bool suppress)
    {
        if (suppress && owner != null)
            _tutorialParrySuppressionOwner = owner;
        else if (_tutorialParrySuppressionOwner == owner)
            _tutorialParrySuppressionOwner = null;
    }

    public override AttackDefense GetEffectiveDefense(AttackDefense baseDefense)
    {
        AttackDefense defense = base.GetEffectiveDefense(baseDefense);
        if (_tutorialParrySuppressionOwner != null)
        {
            defense.Parryable = false;
            defense.FreeBlockOnParryTiming = false;
        }
        return defense;
    }

    protected override void Awake()
    {
        base.Awake();
        _flyingAttack = GetComponent<FlyingMeleeAttackController>();
        _groundAttackBox = _attackCollider as BoxCollider2D;
        if (TryGetComponent<EnemyGroundMove>(out _) && _groundAttackBox == null)
        {
            Debug.LogError("EnemyMeleeCombat: 지상 공격 높이 확인에는 BoxCollider2D가 필요합니다.", this);
        }
        _attackCollider.enabled = false;
        _currentDamage = Enemy != null && Enemy.Data != null ? Enemy.Data.MeleeAttackDamage : 0f;

        _playerFilter = new ContactFilter2D();
        _playerFilter.SetLayerMask(Constants.Layers.PlayerMask);
        _playerFilter.useTriggers = true;
    }

    /// <summary>
    /// 이 공격에 처리된 대상이 있어 몸통 접촉 데미지를 잠시 주지 않는 중인지
    /// (공격 판정이 켜져 있는 동안 + 공격이 끝난 뒤 Contact Damage Grace After Attack 동안)
    /// </summary>
    public bool SuppressesContactDamage =>
        _hasResolvedTarget && (_attackCollider.enabled || Time.time < _contactGraceUntil);

    /// <summary>새 공격 시작: 이번 공격에서 처리된 대상 기록 초기화</summary>
    private void BeginAttackWindow()
    {
        _resolvedTargets.Clear();
        _hasResolvedTarget = false;
    }

    /// <summary>공격 판정이 꺼짐: 처리된 대상이 있으면 그때부터 몸통 접촉 데미지 유예</summary>
    private void EndAttackWindow()
    {
        if (_hasResolvedTarget)
        {
            _contactGraceUntil = Time.time + _contactDamageGraceAfterAttack;
        }
    }

    private void MarkResolved(IDamageable target)
    {
        _resolvedTargets.Add(target);
        _hasResolvedTarget = true;
    }

    /// <summary>
    /// 회피 무적 중에는 플레이어와 EnemyAttack 레이어의 충돌 자체가 꺼져 트리거 이벤트가 오지 않음.
    /// 판정이 켜져 있는 동안 직접 겹침을 확인해서, 무적(회피) 중 판정 안에 있었던 대상은 '회피함'으로 처리
    /// (무적이 끝나는 순간 다시 충돌이 켜지며 들어오는 트리거로 맞지 않도록)
    /// </summary>
    private void FixedUpdate()
    {
        if (!_oneResolvePerAttack || !_attackCollider.enabled)
        {
            return;
        }

        _overlapResults.Clear();
        int count = _attackCollider.Overlap(_playerFilter, _overlapResults);
        for (int i = 0; i < count; i++)
        {
            Collider2D other = _overlapResults[i];
            if (!other.TryGetComponent(out IDamageable damageable) || _resolvedTargets.Contains(damageable))
            {
                continue;
            }

            PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null && playerHealth.IsInvincible)
            {
                MarkResolved(damageable);
            }
        }
    }

    public override void Attack()
    {
        if (_attackCoroutine != null) return;
        CalculateDamage(DamageIncreaseValue);
        BeginAttackWindow();

        _attackCoroutine = StartCoroutine(AttackRoutine());
    }

    // 공중 돌진은 공격 상태가 판정 수명을 관리한다. 지상의 시간 기반 Attack은 유지한다.
    public void StartControlledAttack()
    {
        StopAttack();
        CalculateDamage(DamageIncreaseValue);
        BeginAttackWindow();
        _attackCollider.enabled = true;
    }

    public bool IsTargetInAttackHeight(Transform target)
    {
        if (target != _heightTarget)
        {
            _heightTarget = target;
            _heightTargetCollider = target != null ? target.GetComponent<Collider2D>() : null;
        }
        if (_heightTargetCollider == null || _groundAttackBox == null)
        {
            return false;
        }
        // 비활성 공격 콜라이더의 bounds는 비어 있으므로 로컬 모양으로 높이를 계산한다.
        float centerY = _groundAttackBox.transform.TransformPoint(_groundAttackBox.offset).y;
        float halfHeight = _groundAttackBox.size.y * Mathf.Abs(_groundAttackBox.transform.lossyScale.y) * 0.5f;
        Bounds targetBounds = _heightTargetCollider.bounds;
        return targetBounds.max.y >= centerY - halfHeight && targetBounds.min.y <= centerY + halfHeight;
    }

    private IEnumerator AttackRoutine()
    {
        _attackCollider.enabled = true;

        yield return new WaitForSeconds(Enemy.Data.AttackDurationTime);

        _attackCollider.enabled = false;
        EndAttackWindow();
        _attackCoroutine = null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 본체 콜라이더로 들어온 트리거 이벤트는 무시하고 AttackZone에 닿은 경우만 처리
        if (!_attackCollider.enabled || !_attackCollider.IsTouching(other)) return;

        if (!other.TryGetComponent<IDamageable>(out IDamageable damageable)) return;

        // 이번 공격에서 이미 처리된 대상(맞음 / 가드 / 패링 / 회피)은 다시 판정하지 않음
        if (_oneResolvePerAttack)
        {
            if (_resolvedTargets.Contains(damageable)) return;
            MarkResolved(damageable);
        }
        // 적 본체 위치 대신 공격 판정 중심 기준으로 계산 (실제 맞은 높이에 가깝게)
        Vector2 hitPoint = other.ClosestPoint(_attackCollider.bounds.center);
        var hitData = new HitData(
            _currentDamage,
            Enemy.Data.KnockbackForce,
            hitPoint,
            Enemy.GetDirectionToPlayer(),
            GetEffectiveDefense(_defense),
            this,
            0,
            _attackName
        );
        hitData.Attacker = Enemy.gameObject;

        // 가드 / 패링으로 막히면 false
        if (damageable.TakeDamage(hitData))
        {
            _attackCollider.enabled = false;
            EndAttackWindow();
            if (_flyingAttack == null && Enemy.MoveType is EnemyFlyingMove flyingMove)
            {
                flyingMove.ApplyHitSlowdown();
            }

            OnAttackHit?.Invoke();
        }
    }

    public void OnParried(HitData hitData, Transform parrier)
    {
        // 패링당하면 공격 판정을 바로 끄고 그로기
        StopAttack();
        Enemy.Animation.PlayParriedHit();
        Enemy.EnterGroggy(_groggyDuration);
    }

    public override void StopAttack()
    {
        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
            _attackCoroutine = null;
        }

        if (_attackCollider.enabled)
        {
            _attackCollider.enabled = false;
            EndAttackWindow();
        }
    }

    private void OnDisable()
    {
        StopAttack();
    }

    public void CalculateDamage(float additionalDamage)
    {
        _currentDamage = Enemy.Data.MeleeAttackDamage + additionalDamage;
    }
}
