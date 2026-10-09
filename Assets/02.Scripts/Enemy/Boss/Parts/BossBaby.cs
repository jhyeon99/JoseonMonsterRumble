using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 올챙이 (독 방울을 터트리면 나옴). 공격 패턴 없이 좌우로 무작위로 돌아다니며 닿으면 약한 접촉 데미지.
/// 플레이어 콤보 3타에 맞으면 (그 공격으로 죽더라도) 보스에게 날아가 부딪혀 기믹 데미지.
/// 그 외 공격은 체력이 깎이고 살짝 밀림. 체력이 0이 되면 사라짐.
/// 레이어는 Enemy (플레이어 근접 공격에 맞도록), Rigidbody2D(Dynamic) + 콜라이더 필요.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class BossBaby : MonoBehaviour, IDamageable
{
    [Header("체력")]
    [SerializeField, Min(1f)]
    private float _maxHp = 60f;

    [Tooltip("생성 직후 공격을 받지 않는 시간 (초). 방울을 터트린 공격(3타 등)에 나오자마자 같이 맞지 않도록. 플레이어 공격 판정 시간보다 길게")]
    [SerializeField, Min(0f)]
    private float _spawnInvincibleTime = 0.4f;

    [Tooltip("죽을 때 생성할 이펙트 (선택)")]
    [SerializeField]
    private GameObject _deathEffect;

    [Header("이동 (좌우 무작위)")]
    [SerializeField, Min(0f)]
    private float _moveSpeed = 2f;

    [Tooltip("방향을 바꾸는 간격 (초, 최소 ~ 최대)")]
    [SerializeField]
    private Vector2 _directionChangeInterval = new Vector2(0.8f, 2f);

    [Tooltip("방향을 바꿀 때 잠깐 멈출 확률 (0 ~ 1)")]
    [SerializeField, Range(0f, 1f)]
    private float _stopChance = 0.2f;

    [Tooltip("맵 끝에서 이만큼 안쪽에서 방향을 돌림")]
    [SerializeField, Min(0f)]
    private float _edgeMargin = 0.5f;

    [Tooltip("이동 방향과 반대로 뒤집히면 체크를 바꿔 주기")]
    [SerializeField]
    private bool _spriteFacesRight = false;

    [Tooltip("0보다 크면 이 시간 뒤 사라짐 (초)")]
    [SerializeField, Min(0f)]
    private float _lifeTime = 0f;

    [Header("접촉 데미지")]
    [SerializeField, Min(0f)]
    private float _contactDamage = 1f;

    [SerializeField, Min(0f)]
    private float _contactKnockback = 3f;

    [SerializeField, Min(0.05f)]
    private float _contactInterval = 0.8f;

    [SerializeField]
    private AttackDefense _defense = new AttackDefense { Parryable = true, GuardGaugeCost = 5f };

    [Header("3타 기믹")]
    [Tooltip("이 콤보 단계에 맞으면 보스에게 날아감 (3 = 3타)")]
    [SerializeField, Range(1, 3)]
    private int _launchComboStep = 3;

    [SerializeField, Min(0.1f)]
    private float _launchSpeed = 16f;

    [Tooltip("보스에게 부딪혔을 때 보스가 받는 데미지")]
    [SerializeField, Min(0f)]
    private float _gimmickDamage = 40f;

    [Tooltip("3타가 아닌 공격에 맞았을 때 밀리는 속도")]
    [SerializeField, Min(0f)]
    private float _nudgeSpeed = 3f;

    [SerializeField]
    private CameraShakeSettings _impactShake = new CameraShakeSettings(0.2f, 0.2f);

    [SerializeField]
    private HitStopSettings _impactHitStop = new HitStopSettings(0.06f, 0.05f);

    [Header("상태 (확인용)")]
    [SerializeField, ReadOnly]
    private float _currentHp;

    private const float GroundNormalThreshold = 0.7f;

    private readonly List<Collider2D> _results = new List<Collider2D>();
    private readonly List<ContactPoint2D> _groundContacts = new List<ContactPoint2D>();
    private ContactFilter2D _groundFilter;
    private Rigidbody2D _body;
    private Collider2D _collider;
    private HitFlash _hitFlash;
    private ContactFilter2D _playerFilter;
    private Boss _boss;
    private float _contactTimer;
    private float _nudgeTimer;
    private float _directionTimer;
    private float _direction;
    private bool _isLaunched;
    private bool _isDead;
    private float _invincibleUntil;

    public void Initialize(Boss boss)
    {
        _boss = boss;
        if (_boss != null)
        {
            _boss.RegisterMinion(gameObject);
        }

        _invincibleUntil = Time.time + _spawnInvincibleTime;

        if (_lifeTime > 0f)
        {
            Destroy(gameObject, _lifeTime);
        }

        PickDirection();
    }

    private void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        _hitFlash = GetComponent<HitFlash>();
        _currentHp = _maxHp;

        _playerFilter = new ContactFilter2D();
        _playerFilter.SetLayerMask(Constants.Layers.PlayerMask);
        _playerFilter.useTriggers = true;

        _groundFilter = new ContactFilter2D();
        _groundFilter.SetLayerMask(Constants.Layers.GroundAllMask);
        _groundFilter.useTriggers = false;
    }

    private void FixedUpdate()
    {
        if (_isLaunched)
        {
            FlyToBoss();
            return;
        }

        Wander();
        CheckContact();
    }

    private void PickDirection()
    {
        float min = Mathf.Max(0.1f, _directionChangeInterval.x);
        _directionTimer = Random.Range(min, Mathf.Max(min, _directionChangeInterval.y));
        _direction = Random.value < _stopChance ? 0f : (Random.value < 0.5f ? -1f : 1f);
    }

    private void Wander()
    {
        if (_nudgeTimer > 0f)
        {
            _nudgeTimer -= Time.fixedDeltaTime;
            return;
        }

        // 방울에서 나와 떨어지는 동안(공중)은 움직이지 않고 그대로 낙하
        if (!IsGrounded())
        {
            _body.linearVelocity = new Vector2(0f, _body.linearVelocity.y);
            return;
        }

        _directionTimer -= Time.fixedDeltaTime;
        if (_directionTimer <= 0f)
        {
            PickDirection();
        }

        // 맵 끝에 닿으면 반대로
        BossArena arena = _boss != null ? _boss.Arena : null;
        if (arena != null)
        {
            float x = _body.position.x;
            if (x <= arena.LeftX + _edgeMargin && _direction < 0f)
            {
                _direction = 1f;
            }
            else if (x >= arena.RightX - _edgeMargin && _direction > 0f)
            {
                _direction = -1f;
            }
        }

        _body.linearVelocity = new Vector2(_direction * _moveSpeed, _body.linearVelocity.y);

        if (_direction != 0f)
        {
            Vector3 scale = transform.localScale;
            float sign = _spriteFacesRight ? -_direction : _direction;
            scale.x = Mathf.Abs(scale.x) * sign;
            transform.localScale = scale;
        }
    }

    /// <summary>땅(Ground / OneWayPlatform)을 밟고 있는지</summary>
    private bool IsGrounded()
    {
        int count = _body.GetContacts(_groundFilter, _groundContacts);
        for (int i = 0; i < count; i++)
        {
            if (_groundContacts[i].normal.y > GroundNormalThreshold)
            {
                return true;
            }
        }

        return false;
    }

    private void CheckContact()
    {
        if (_contactTimer > 0f)
        {
            _contactTimer -= Time.fixedDeltaTime;
            return;
        }

        if (_collider == null || _contactDamage <= 0f)
        {
            return;
        }

        _results.Clear();
        int count = Physics2D.OverlapCollider(_collider, _playerFilter, _results);

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

            Vector2 direction = ((Vector2)other.bounds.center - (Vector2)transform.position).normalized;
            var hitData = new HitData(
                _contactDamage,
                _contactKnockback,
                other.ClosestPoint(transform.position),
                direction,
                _defense
            );

            damageable.TakeDamage(hitData);
            _contactTimer = _contactInterval;
            return;
        }
    }

    public bool TakeDamage(HitData hitData)
    {
        if (_isLaunched || _isDead)
        {
            return false;
        }

        // 방울을 터트린 그 공격에 같이 맞지 않도록 생성 직후 잠깐 무적
        if (Time.time < _invincibleUntil)
        {
            return false;
        }

        if (_hitFlash != null)
        {
            _hitFlash.Flash();
        }

        _currentHp = Mathf.Max(0f, _currentHp - hitData.Damage);

        // 3타는 죽든 말든 보스에게 날아감
        if (hitData.ComboStep == _launchComboStep)
        {
            Launch();
            return true;
        }

        if (_currentHp <= 0f)
        {
            Die();
            return true;
        }

        // 그 외 공격은 살짝 밀림
        float pushX = hitData.AttackDirection.x >= 0f ? 1f : -1f;
        _body.linearVelocity = new Vector2(pushX * _nudgeSpeed, _body.linearVelocity.y);
        _nudgeTimer = 0.15f;
        return true;
    }

    private void Die()
    {
        _isDead = true;

        if (_deathEffect != null)
        {
            Instantiate(_deathEffect, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }

    private void Launch()
    {
        if (_boss == null || !_boss.isActiveAndEnabled)
        {
            Die();
            return;
        }

        _isLaunched = true;
        _body.bodyType = RigidbodyType2D.Kinematic;
        _body.linearVelocity = Vector2.zero;

        if (_collider != null)
        {
            _collider.isTrigger = true;
        }
    }

    private void FlyToBoss()
    {
        if (_boss == null || !_boss.isActiveAndEnabled)
        {
            Destroy(gameObject);
            return;
        }

        Vector2 target = _boss.CenterPosition;
        var next = Vector2.MoveTowards(_body.position, target, _launchSpeed * Time.fixedDeltaTime);
        _body.MovePosition(next);

        // 회전하며 날아감
        transform.Rotate(0f, 0f, 720f * Time.fixedDeltaTime);

        if ((next - target).sqrMagnitude <= 0.04f)
        {
            HitBoss();
        }
    }

    private void HitBoss()
    {
        if (_boss.Health.TakeGimmickDamage(_gimmickDamage))
        {
            CameraShake.Shake(_impactShake);
            HitStop.Play(_impactHitStop);
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (_boss != null)
        {
            _boss.UnregisterMinion(gameObject);
        }
    }
}
