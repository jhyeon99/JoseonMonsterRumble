using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 공격 판정. 보스 자식 오브젝트에 콜라이더(Trigger)와 함께 붙임 (레이어 EnemyAttack 권장).
/// 패턴이 Activate / Deactivate로 켜고 끔. 한 번 켤 때 플레이어를 한 번만 때림.
/// 패링당하면 판정이 꺼지고, _groggyOnParry가 켜져 있으면 보스가 그로기 (찍기).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BossHitbox : MonoBehaviour, IParryable
{
    [Header("데미지")]
    [Tooltip("공격 이름 (사망 원인 표시용). 비우면 오브젝트 이름 사용")]
    [SerializeField]
    private string _attackName = "";

    [SerializeField, Min(0f)] private float _damage = 20f;
    [SerializeField, Min(0f)] private float _knockbackForce = 8f;

    [Header("가드 / 패링")]
    [SerializeField]
    private AttackDefense _defense = new AttackDefense { Parryable = true, GuardGaugeCost = 40f };

    [Tooltip("패링당하면 보스가 그로기 (찍기 패턴용)")]
    [SerializeField]
    private bool _groggyOnParry = false;

    [Tooltip("패링 그로기 시간 (초). 보스가 가만히 있는 프리딜 시간")]
    [SerializeField, Min(0f)]
    private float _groggyDuration = 3f;

    [Header("예고 표시 (선택)")]
    [Tooltip("공격 전 위험 범위를 보여줄 오브젝트 (반투명 스프라이트 등). 비워도 됨")]
    [SerializeField]
    private GameObject _telegraph;

    /// <summary>패링당함</summary>
    public event Action OnParriedByPlayer;
    public event Action<HitData> OnPlayerDamaged;

    private readonly List<Collider2D> _results = new List<Collider2D>();
    private Collider2D _collider;
    private Boss _boss;
    private ContactFilter2D _filter;
    private bool _isActive;
    private bool _hasHit;

    public bool IsActive => _isActive;

    /// <summary>패링당하면 보스가 그로기가 되는 판정인지 (반짝임 예고 대상)</summary>
    public bool GroggyOnParry => _groggyOnParry;

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        _collider.isTrigger = true;
        _collider.enabled = false;
        _boss = GetComponentInParent<Boss>();

        _filter = new ContactFilter2D();
        _filter.SetLayerMask(Constants.Layers.PlayerMask);
        _filter.useTriggers = true;

        SetTelegraph(false);
    }

    private void OnDisable()
    {
        Deactivate();
        SetTelegraph(false);
    }

    public void SetTelegraph(bool show)
    {
        if (_telegraph != null)
        {
            _telegraph.SetActive(show);
        }
    }

    /// <summary>판정 켜기</summary>
    public void Activate()
    {
        _isActive = true;
        _hasHit = false;
        _collider.enabled = true;
        CheckHit();
    }

    /// <summary>판정 끄기</summary>
    public void Deactivate()
    {
        _isActive = false;
        if (_collider != null)
        {
            _collider.enabled = false;
        }
    }

    private void FixedUpdate()
    {
        if (_isActive && !_hasHit)
        {
            CheckHit();
        }
    }

    private void CheckHit()
    {
        _results.Clear();
        int count = Physics2D.OverlapCollider(_collider, _filter, _results);

        for (int i = 0; i < count; i++)
        {
            Collider2D other = _results[i];
            if (other.GetComponentInParent<Player>() == null)
            {
                continue;
            }

            IDamageable damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null)
            {
                continue;
            }

            _hasHit = true;

            Vector2 origin = _boss != null ? _boss.CenterPosition : (Vector2)transform.position;
            Vector2 direction = ((Vector2)other.bounds.center - origin).normalized;
            var hitData = new HitData(
                _damage,
                _knockbackForce,
                other.ClosestPoint(_collider.bounds.center),
                direction,
                _defense,
                this,
                attackName: string.IsNullOrWhiteSpace(_attackName) ? name : _attackName
            );
            hitData.Attacker = _boss != null ? _boss.gameObject : gameObject;

            bool damageAccepted = damageable.TakeDamage(hitData);
            if (damageAccepted && hitData.Damage > 0f)
            {
                OnPlayerDamaged?.Invoke(hitData);
            }
            return;
        }
    }

    public void OnParried(HitData hitData, Transform parrier)
    {
        Deactivate();
        OnParriedByPlayer?.Invoke();

        if (_groggyOnParry && _boss != null)
        {
            _boss.EnterGroggy(_groggyDuration);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Collider2D col = _collider != null ? _collider : GetComponent<Collider2D>();
        if (col == null)
        {
            return;
        }

        Gizmos.color = _isActive ? new Color(1f, 0f, 0f, 0.8f) : new Color(1f, 0.5f, 0f, 0.5f);
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
}
