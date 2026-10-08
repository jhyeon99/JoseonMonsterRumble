using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 소환된 아기. 공격 패턴 없이 플레이어 쪽으로 걸어오며 닿으면 접촉 데미지.
/// 플레이어 콤보 3타에 맞으면 보스에게 날아가 부딪혀 기믹 데미지. 그 외 공격은 살짝 밀리기만 함.
/// 레이어는 Enemy (플레이어 근접 공격에 맞도록), Rigidbody2D(Dynamic) + 콜라이더 필요.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class BossBaby : MonoBehaviour, IDamageable
{
    [Header("이동")]
    [SerializeField, Min(0f)]
    private float _moveSpeed = 2.5f;

    [Tooltip("스프라이트 원본이 오른쪽을 보고 있으면 체크")]
    [SerializeField]
    private bool _spriteFacesRight = false;

    [Tooltip("0보다 크면 이 시간 뒤 사라짐 (초)")]
    [SerializeField, Min(0f)]
    private float _lifeTime = 0f;

    [Header("접촉 데미지")]
    [SerializeField, Min(0f)]
    private float _contactDamage = 10f;

    [SerializeField, Min(0f)]
    private float _contactKnockback = 5f;

    [SerializeField, Min(0.05f)]
    private float _contactInterval = 0.8f;

    [SerializeField]
    private AttackDefense _defense = new AttackDefense { Parryable = true, GuardGaugeCost = 20f };

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

    private readonly List<Collider2D> _results = new List<Collider2D>();
    private Rigidbody2D _body;
    private Collider2D _collider;
    private HitFlash _hitFlash;
    private ContactFilter2D _playerFilter;
    private Boss _boss;
    private Transform _player;
    private float _contactTimer;
    private float _nudgeTimer;
    private bool _isLaunched;

    public void Initialize(Boss boss)
    {
        _boss = boss;
        if (_boss != null)
        {
            _boss.RegisterMinion(gameObject);
            _player = _boss.Player;
        }

        if (_player == null)
        {
            var player = GameObject.FindWithTag(Constants.Tags.Player);
            _player = player != null ? player.transform : null;
        }

        if (_lifeTime > 0f)
        {
            Destroy(gameObject, _lifeTime);
        }
    }

    private void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        _hitFlash = GetComponent<HitFlash>();

        _playerFilter = new ContactFilter2D();
        _playerFilter.SetLayerMask(Constants.Layers.PlayerMask);
        _playerFilter.useTriggers = true;
    }

    private void FixedUpdate()
    {
        if (_isLaunched)
        {
            FlyToBoss();
            return;
        }

        Walk();
        CheckContact();
    }

    private void Walk()
    {
        if (_nudgeTimer > 0f)
        {
            _nudgeTimer -= Time.fixedDeltaTime;
            return;
        }

        if (_player == null)
        {
            _body.linearVelocity = new Vector2(0f, _body.linearVelocity.y);
            return;
        }

        float diff = _player.position.x - transform.position.x;
        float direction = Mathf.Abs(diff) > 0.1f ? Mathf.Sign(diff) : 0f;
        _body.linearVelocity = new Vector2(direction * _moveSpeed, _body.linearVelocity.y);

        if (direction != 0f)
        {
            Vector3 scale = transform.localScale;
            float sign = _spriteFacesRight ? direction : -direction;
            scale.x = Mathf.Abs(scale.x) * sign;
            transform.localScale = scale;
        }
    }

    private void CheckContact()
    {
        if (_contactTimer > 0f)
        {
            _contactTimer -= Time.fixedDeltaTime;
            return;
        }

        if (_collider == null)
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
        if (_isLaunched)
        {
            return false;
        }

        if (_hitFlash != null)
        {
            _hitFlash.Flash();
        }

        if (hitData.ComboStep == _launchComboStep)
        {
            Launch();
            return true;
        }

        // 그 외 공격은 살짝 밀리기만 함
        float pushX = hitData.AttackDirection.x >= 0f ? 1f : -1f;
        _body.linearVelocity = new Vector2(pushX * _nudgeSpeed, _body.linearVelocity.y);
        _nudgeTimer = 0.15f;
        return true;
    }

    private void Launch()
    {
        if (_boss == null || !_boss.isActiveAndEnabled)
        {
            Destroy(gameObject);
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