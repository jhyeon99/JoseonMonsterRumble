using UnityEngine;

public class PlayerProjectile : Projectile
{
    [Header("탐색")]
    [SerializeField] private float _searchRadius = 8f;

    [SerializeField] private LayerMask _enemyLayer;

    [SerializeField] private LayerMask _obstacleLayer; // 시야를 가리는 레이어 (Ground, Wall 등)

    [SerializeField] private Vector2 _sightOffset = new Vector2(0f, 0.5f); // 플레이어 피벗 기준 시야 위치

    [SerializeField] private bool _drawDebug = false;

    [Header("유도")]
    [SerializeField] private float _turnSpeed = 360f; // 초당 회전 각도(도)

    [SerializeField] private float _lifeTime = 3f;

    private static readonly Collider2D[] Hits = new Collider2D[16];

    private Rigidbody2D _rigidbody2D;
    private Transform _target;
    private Vector2 _dir;
    private int _facing = 1; // 1 = 오른쪽, -1 = 왼쪽
    private bool _initialized;

    // ※ Awake / Update / OnTriggerEnter2D 선언 금지 (기반 클래스 것이 private이라 덮어써짐)

    /// <summary>타겟이 없을 때 날아갈 방향. OnSpawn 전에 호출</summary>
    public void SetFacing(int facing) => _facing = facing >= 0 ? 1 : -1;

    protected override void Fire()
    {
        if (!_isLaunching) return;

        if (!_initialized) Initialize();

        // 고정된 대상이 살아 있는 동안만 유도
        if (_target != null && _target.gameObject.activeInHierarchy)
        {
            Vector2 toTarget = ((Vector2)_target.position - _rigidbody2D.position).normalized;
            float maxRadians = _turnSpeed * Mathf.Deg2Rad * Time.deltaTime;
            _dir = Vector3.RotateTowards(_dir, toTarget, maxRadians, 0f);
        }
        else
        {
            _target = null; // 대상이 죽거나 풀로 돌아가면 그대로 직진
        }

        ApplyVelocity();

        _t += Time.deltaTime;
        if (_t >= _lifeTime)
        {
            _isLaunching = false;
            gameObject.SetActive(false);
        }
    }

    // 발사 직후 첫 프레임에 한 번만 실행: 주변 적 탐색 후 target 고정
    private void Initialize()
    {
        if (_rigidbody2D == null) _rigidbody2D = GetComponent<Rigidbody2D>();

        _target = FindNearestEnemy();

        if (_target != null)
        {
            // 적을 찾았으면 적 방향으로 출발
            _dir = ((Vector2)_target.position - (Vector2)transform.position).normalized;
        }
        else
        {
            // 못 찾았으면 플레이어가 넘겨준 방향으로 직진
            _dir = _facing > 0 ? Vector2.right : Vector2.left;
        }

        _initialized = true;
    }

    // 범위 안에서 시야가 확보된 적 중 가장 가까운 적
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
            if (!col.TryGetComponent<IDamageable>(out _)) continue;

            Vector2 enemyPos = col.bounds.center;
            float sqr = (enemyPos - origin).sqrMagnitude;

            // 이미 찾은 적보다 멀면 시야 검사할 필요 없음
            if (sqr >= minSqr) continue;

            // 사이에 장애물이 있으면 감지 안 됨
            if (!HasLineOfSight(origin, enemyPos)) continue;

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
            Debug.DrawLine(from, to, visible ? Color.green : Color.red, 1f);

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
        // 풀로 돌아갈 때 초기화 → 다음 발사 때 다시 탐색
        _initialized = false;
        _target = null;
        _facing = 1;
        if (_rigidbody2D != null)
        {
            _rigidbody2D.linearVelocity = Vector2.zero;
        }
    }
}