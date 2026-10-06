using System.Collections;
using UnityEngine;

public class PlayerProjectile : Projectile
{
    private const float ExplosionDuration = 0.5f;

    private static readonly int AnimParamFire = Animator.StringToHash("fire");
    private static readonly int AnimParamExplode = Animator.StringToHash("explode");
    private static readonly Collider2D[] Hits = new Collider2D[16];

    [Header("탐색")]
    [SerializeField] private float _searchRadius = 8f;
    [SerializeField] private LayerMask _enemyLayer;
    [SerializeField] private LayerMask _obstacleLayer;
    [SerializeField] private Vector2 _sightOffset = new Vector2(0f, 0.5f);
    [SerializeField] private bool _drawDebug = false;

    [Header("유도")]
    [SerializeField] private float _turnSpeed = 360f;
    [SerializeField] private float _lifeTime = 3f;

    private Rigidbody2D _rigidbody2D;
    private Collider2D _collider2D;
    private Animator _animator;
    private Transform _target;
    private Vector2 _dir;
    private int _facing = 1;
    private bool _initialized;
    private bool _isExploding;
    private Coroutine _explodeRoutine;

    public void SetFacing(int facing) => _facing = facing >= 0 ? 1 : -1;

    protected override Vector2 HitDirection =>
        _dir != Vector2.zero ? _dir : (_facing > 0 ? Vector2.right : Vector2.left);

    public new void OnSpawn(Transform muzzlePoint, Transform playerPoint, float damageIncreaseValue)
    {
        base.OnSpawn(muzzlePoint, playerPoint, damageIncreaseValue);

        EnsureComponents();

        _isExploding = false;
        if (_collider2D != null)
        {
            _collider2D.enabled = true;
        }

        if (_animator != null)
        {
            _animator.SetTrigger(AnimParamFire);
        }
    }

    protected override void Fire()
    {
        if (!_isLaunching || _isExploding)
        {
            return;
        }

        if (!_initialized)
        {
            Initialize();
        }

        if (_target != null && _target.gameObject.activeInHierarchy)
        {
            Vector2 toTarget = ((Vector2)_target.position - _rigidbody2D.position).normalized;
            float maxRadians = _turnSpeed * Mathf.Deg2Rad * Time.deltaTime;
            _dir = Vector3.RotateTowards(_dir, toTarget, maxRadians, 0f);
        }
        else
        {
            _target = null;
        }

        ApplyVelocity();

        _t += Time.deltaTime;
        if (_t >= _lifeTime)
        {
            _isLaunching = false;
            gameObject.SetActive(false);
        }
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (_isExploding)
        {
            return;
        }

        if (!other.TryGetComponent<IDamageable>(out IDamageable damageable))
        {
            return;
        }

        Vector2 hitPoint = other.ClosestPoint(transform.position);
        var hitData = new HitData(
            ProjectileDamage,
            1f,
            hitPoint,
            HitDirection
        );

        damageable.TakeDamage(hitData);

        StartExplosion();
    }

    private void StartExplosion()
    {
        _isExploding = true;
        _isLaunching = false;

        if (_collider2D != null)
        {
            _collider2D.enabled = false;
        }

        if (_rigidbody2D != null)
        {
            _rigidbody2D.linearVelocity = Vector2.zero;
        }

        if (_animator != null)
        {
            _animator.SetTrigger(AnimParamExplode);
        }

        if (_explodeRoutine != null)
        {
            StopCoroutine(_explodeRoutine);
        }

        _explodeRoutine = StartCoroutine(WaitForExplosionRoutine());
    }

    private IEnumerator WaitForExplosionRoutine()
    {
        yield return new WaitForSeconds(ExplosionDuration);

        _explodeRoutine = null;
        gameObject.SetActive(false);
    }

    private void EnsureComponents()
    {
        if (_rigidbody2D == null)
        {
            _rigidbody2D = GetComponent<Rigidbody2D>();
        }

        if (_collider2D == null)
        {
            _collider2D = GetComponent<Collider2D>();
        }

        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>();
        }
    }

    private void Initialize()
    {
        EnsureComponents();

        _target = FindNearestEnemy();

        if (_target != null)
        {
            _dir = ((Vector2)_target.position - (Vector2)transform.position).normalized;
        }
        else
        {
            _dir = _facing > 0 ? Vector2.right : Vector2.left;
        }

        _initialized = true;
    }

    private Transform FindNearestEnemy()
    {
        var filter = new ContactFilter2D();
        filter.SetLayerMask(_enemyLayer);
        filter.useTriggers = true;

        Vector2 origin = PlayerPosition + _sightOffset;
        int count = Physics2D.OverlapCircle(origin, _searchRadius, filter, Hits);

        Transform nearest = null;
        float minSqr = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider2D col = Hits[i];
            if (!col.TryGetComponent<IDamageable>(out _))
            {
                continue;
            }

            Vector2 enemyPos = col.bounds.center;
            float sqr = (enemyPos - origin).sqrMagnitude;

            if (sqr >= minSqr)
            {
                continue;
            }

            if (!HasLineOfSight(origin, enemyPos))
            {
                continue;
            }

            minSqr = sqr;
            nearest = col.transform;
        }

        return nearest;
    }

    private bool HasLineOfSight(Vector2 from, Vector2 to)
    {
        RaycastHit2D hit = Physics2D.Linecast(from, to, _obstacleLayer);
        bool visible = hit.collider == null;

        if (_drawDebug)
        {
            Debug.DrawLine(from, to, visible ? Color.green : Color.red, 1f);
        }

        return visible;
    }

    private void ApplyVelocity()
    {
        _rigidbody2D.linearVelocity = _dir * Speed;

        float angle = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void OnDisable()
    {
        if (_explodeRoutine != null)
        {
            StopCoroutine(_explodeRoutine);
            _explodeRoutine = null;
        }

        _isExploding = false;
        _initialized = false;
        _target = null;
        _facing = 1;

        if (_collider2D != null)
        {
            _collider2D.enabled = true;
        }

        if (_rigidbody2D != null)
        {
            _rigidbody2D.linearVelocity = Vector2.zero;
        }
    }
}