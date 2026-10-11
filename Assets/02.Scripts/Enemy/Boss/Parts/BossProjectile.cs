using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스가 쏘는 직선 투사체 (찍기 후 좌우 바늘 등). Launch(방향)으로 발사.
/// 콜라이더(Trigger) 필요. 플레이어에게 맞거나 시간이 지나면 사라짐. 패링당하면 사라짐.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BossProjectile : MonoBehaviour, IParryable
{
    [Header("이동")]
    [SerializeField, Min(0f)] private float _speed = 10f;
    [SerializeField, Min(0.1f)] private float _lifeTime = 3f;

    [Tooltip("스프라이트 원본이 오른쪽을 보고 있으면 체크 (진행 방향으로 회전)")]
    [SerializeField]
    private bool _spriteFacesRight = true;

    [Header("데미지")]
    [Tooltip("공격 이름 (사망 원인 표시용)")]
    [SerializeField]
    private string _attackName = "가시";

    [SerializeField, Min(0f)] private float _damage = 15f;
    [SerializeField, Min(0f)] private float _knockbackForce = 6f;

    [SerializeField]
    private AttackDefense _defense = new AttackDefense { Parryable = true, GuardGaugeCost = 30f };

    private readonly List<Collider2D> _results = new List<Collider2D>();
    private Collider2D _collider;
    private ContactFilter2D _filter;
    private Vector2 _direction = Vector2.right;
    private float _timer;
    private GameObject _attacker;

    /// <summary>쏜 대상 (사망 원인 표시용)</summary>
    public void SetAttacker(GameObject attacker)
    {
        _attacker = attacker;
    }

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        _collider.isTrigger = true;

        _filter = new ContactFilter2D();
        _filter.SetLayerMask(Constants.Layers.PlayerMask);
        _filter.useTriggers = true;
    }

    /// <summary>발사. speed가 0보다 크면 기본 속도 대신 사용</summary>
    public void Launch(Vector2 direction, float speed = -1f)
    {
        _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        if (speed > 0f)
        {
            _speed = speed;
        }

        _timer = _lifeTime;

        float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
        if (!_spriteFacesRight)
        {
            angle += 180f;
        }

        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void Update()
    {
        transform.position += (Vector3)(_direction * (_speed * Time.deltaTime));

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        CheckHit();
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

            if (damageable is PlayerHealth playerHealth && playerHealth.IsInvincible)
            {
                continue;
            }

            var hitData = new HitData(
                _damage,
                _knockbackForce,
                other.ClosestPoint(transform.position),
                _direction,
                _defense,
                this,
                attackName: _attackName
            );
            hitData.Attacker = _attacker;

            damageable.TakeDamage(hitData);
            Destroy(gameObject);
            return;
        }
    }

    public void OnParried(HitData hitData, Transform parrier)
    {
        Destroy(gameObject);
    }
}
