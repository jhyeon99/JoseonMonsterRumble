using UnityEngine;

public class EnemyBuffMove : EnemyMoveType
{
    [Header("동적 공전 마진 및 제한")]
    [SerializeField] private float _orbitMarginX = 0.85f;
    [SerializeField] private float _orbitMarginY = 0.55f;
    [SerializeField] private float _minOrbitRadiusX = 1.8f;
    [SerializeField] private float _minOrbitRadiusY = 1.2f;
    [SerializeField] private float _maxOrbitRadiusX = 2.2f;
    [SerializeField] private float _maxOrbitRadiusY = 1.6f;

    [Header("공전 물리 파라미터")]
    [SerializeField] private float _orbitSpeed = 100f;
    [SerializeField] private float _floatAmplitude = 0.25f;
    [SerializeField] private float _floatFrequency = 2.5f;
    [SerializeField] private float _followLerpSpeed = 8.5f;
    [SerializeField] private float _decelerateDistance = 5f;
    [SerializeField] private float _arrivalDistance = 0.45f;

    private float _orbitAngle;
    private float _orbitDirection = 1f;
    private float _floatTime;
    private bool _isOrbitInitialized;

    private Enemy _cachedTarget;
    private SpriteRenderer _cachedTargetRenderer;
    private Collider2D _cachedTargetCollider;

    protected override void Awake()
    {
        base.Awake();
        InitializeOrbit();
    }

    private void InitializeOrbit()
    {
        if (_isOrbitInitialized)
        {
            return;
        }

        _orbitDirection = Random.value > 0.5f ? 1f : -1f;
        _orbitAngle = Random.Range(0f, 360f);
        _floatTime = Random.Range(0f, Mathf.PI * 2f);
        _isOrbitInitialized = true;
    }

    public override void Move(Vector2 direction)
    {
        Rigidbody.linearVelocity = direction * Enemy.Data.MoveSpeed;
    }

    public override void BackMove(Vector2 direction)
    {
    }

    private void CacheTargetComponents(Enemy target)
    {
        if (_cachedTarget == target && _cachedTargetRenderer != null)
        {
            return;
        }

        _cachedTarget = target;
        _cachedTargetRenderer = target != null ? target.GetComponentInChildren<SpriteRenderer>() : null;
        _cachedTargetCollider = target != null ? target.GetComponent<Collider2D>() : null;
    }

    private Vector2 GetTargetCenter(Enemy target)
    {
        CacheTargetComponents(target);

        if (_cachedTargetRenderer != null && _cachedTargetRenderer.sprite != null)
        {
            return _cachedTargetRenderer.bounds.center;
        }

        return target.GetEnemyCenterPosition();
    }

    private Vector2 CalculateDynamicOrbitRadius(Enemy target)
    {
        CacheTargetComponents(target);

        float halfWidth = 0.5f;
        float halfHeight = 0.5f;

        if (_cachedTargetRenderer != null && _cachedTargetRenderer.sprite != null)
        {
            Bounds spriteBounds = _cachedTargetRenderer.bounds;
            halfWidth = Mathf.Max(halfWidth, spriteBounds.extents.x);
            halfHeight = Mathf.Max(halfHeight, spriteBounds.extents.y);
        }

        if (_cachedTargetCollider != null)
        {
            Bounds colliderBounds = _cachedTargetCollider.bounds;
            halfWidth = Mathf.Max(halfWidth, colliderBounds.extents.x);
            halfHeight = Mathf.Max(halfHeight, colliderBounds.extents.y);
        }

        float radiusX = Mathf.Clamp(halfWidth + _orbitMarginX, _minOrbitRadiusX, _maxOrbitRadiusX);
        float radiusY = Mathf.Clamp(halfHeight + _orbitMarginY, _minOrbitRadiusY, _maxOrbitRadiusY);
        return new Vector2(radiusX, radiusY);
    }

    private Vector2 CalculateOrbitPosition(Enemy target, float angleDeg, float floatTime)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        float floatOffset = Mathf.Sin(floatTime * _floatFrequency) * _floatAmplitude;

        Vector2 center = GetTargetCenter(target);
        Vector2 dynamicRadius = CalculateDynamicOrbitRadius(target);

        var offset = new Vector2(
            Mathf.Cos(rad) * dynamicRadius.x,
            (Mathf.Sin(rad) * dynamicRadius.y) + floatOffset
        );

        return center + offset;
    }

    private Vector2 GetChaseStartPosition(Enemy target)
    {
        InitializeOrbit();
        return CalculateOrbitPosition(target, _orbitAngle, _floatTime);
    }

    public override void MoveToStartPosition(Enemy target)
    {
        if (target == null)
        {
            return;
        }

        Vector2 startWorldPos = GetChaseStartPosition(target);
        float direction = target.transform.position.x - transform.position.x;
        Flip(direction);

        float distance = Vector2.Distance(Rigidbody.position, startWorldPos);
        float minSpeed = Enemy.Data.MoveToEnemySpeed * 0.15f;
        float currentSpeed = Mathf.Lerp(minSpeed, Enemy.Data.MoveToEnemySpeed, distance / _decelerateDistance);

        var smoothPos = Vector2.MoveTowards(Rigidbody.position, startWorldPos, currentSpeed * Time.fixedDeltaTime);
        Rigidbody.MovePosition(smoothPos);

        if (distance <= _arrivalDistance)
        {
            Enemy.StateMachine.ChangeState(new BuffState(Enemy, target));
        }
    }

    public override void ChaseMove(Enemy target)
    {
        if (target == null)
        {
            return;
        }

        InitializeOrbit();

        _orbitAngle += _orbitDirection * _orbitSpeed * Time.fixedDeltaTime;
        if (_orbitAngle >= 360f)
        {
            _orbitAngle -= 360f;
        }
        else if (_orbitAngle < 0f)
        {
            _orbitAngle += 360f;
        }

        _floatTime += Time.fixedDeltaTime;

        Vector2 targetWorldPos = CalculateOrbitPosition(target, _orbitAngle, _floatTime);
        var smoothPos = Vector2.Lerp(Rigidbody.position, targetWorldPos, _followLerpSpeed * Time.fixedDeltaTime);

        Rigidbody.MovePosition(smoothPos);

        float direction = target.transform.position.x - transform.position.x;
        Flip(direction);
    }

    public override void StopMove()
    {
        Rigidbody.linearVelocity = Vector2.zero;
    }

    private void Flip(float direction)
    {
        if (Mathf.Abs(direction) < 0.01f)
        {
            return;
        }

        Vector3 scale = transform.localScale;
        if (direction > 0f)
        {
            scale.x = Mathf.Abs(scale.x);
        }
        else if (direction < 0f)
        {
            scale.x = -Mathf.Abs(scale.x);
        }

        transform.localScale = scale;
    }
}
