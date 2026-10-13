using System.Collections;
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
    private Coroutine _attackCoroutine;
    private float _currentDamage;
    private Transform _heightTarget;
    private Collider2D _heightTargetCollider;
    private BoxCollider2D _groundAttackBox;
    private FlyingMeleeAttackController _flyingAttack;
    public float CurrentDamage => _currentDamage;
    public FlyingMeleeAttackController FlyingAttack => _flyingAttack;
    public event Action OnAttackHit;

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
    }

    public override void Attack()
    {
        if (_attackCoroutine != null) return;
        CalculateDamage(DamageIncreaseValue);

        _attackCoroutine = StartCoroutine(AttackRoutine());
    }

    // 공중 돌진은 공격 상태가 판정 수명을 관리한다. 지상의 시간 기반 Attack은 유지한다.
    public void StartControlledAttack()
    {
        StopAttack();
        CalculateDamage(DamageIncreaseValue);
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

        _attackCollider.enabled = false;
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
