using System;
using UnityEngine;

public abstract class Projectile : MonoBehaviour, IParryable
{
    [SerializeField] private ProjectileData _data;

    [Tooltip("비어 있으면 ProjectileData의 AttackName 또는 기본값을 사용합니다.")]
    [SerializeField]
    private string _attackName;

    [SerializeField] private ProjectileType _type;
    [SerializeField] private float _curveHeight = 5f;
    [SerializeField] private float _returnPoolTimer = 5f;

    [Header("가드 / 패링")]

    [SerializeField] private AttackDefense _defense = new AttackDefense { Parryable = true, GuardGaugeCost = 25f };

    [Header("패링 반사 (속도 / 데미지 배율은 PlayerGuard에서 설정)")]

    [Tooltip("켜면 쏜 적의 몸(콜라이더 중심)을 따라가며 되돌아감. 끄거나 쏜 적이 없으면 맞은 방향의 반대로 직진")]
    [SerializeField]
    private bool _reflectToShooter = true;

    [Tooltip("쏜 적에게 되돌아가는 동안 벽 / 발판에 막히지 않고 통과")]
    [SerializeField]
    private bool _ignoreGroundWhenReturning = true;

    private float _returnPoolTime = 0f;
    public ProjectileType Type => _type;
    protected float Speed { get; private set; }
    protected Vector2 ControlPoint { get; private set; }
    protected Vector2 MuzzlePosition { get; private set; }
    protected Vector2 PlayerPosition { get; private set; }
    protected float _t;
    protected bool _isLaunching = false;
    private float _projectileDamage;
    public float ProjectileDamage => _projectileDamage;
    protected float KnockbackForce => _data.KnockbackForce;
    public string AttackName => !string.IsNullOrWhiteSpace(_attackName)
        ? _attackName
        : (_data != null && !string.IsNullOrWhiteSpace(_data.AttackName) ? _data.AttackName : "원거리 공격");

    // 반사 상태
    private int _originalLayer;
    private Transform _muzzleTransform;
    private Collider2D _shooterCollider;
    private bool _isReflected;
    private Vector2 _reflectDirection;
    private float _reflectSpeed;
    public bool IsReflected => _isReflected;

    private void Awake()
    {
        Speed = _data.ProjectileSpeed;
        _projectileDamage = _data.ProjectileDamage;
        _returnPoolTime = _returnPoolTimer;
        _originalLayer = gameObject.layer;
    }

    private void Update()
    {
        if (_isReflected)
        {
            MoveReflected();
        }
        else
        {
            Fire();
        }

        ReturnProjectile();
    }

    private void ReturnProjectile()
    {
        _returnPoolTime -= Time.deltaTime;
        if (_returnPoolTime <= 0f)
        {
            gameObject.SetActive(false);
            _returnPoolTime = _returnPoolTimer;
        }
    }

    public virtual void OnSpawn(Transform muzzlePoint, Transform playerPoint, float damageIncreaseValue)
    {
        MuzzlePosition = muzzlePoint.position;
        _muzzleTransform = muzzlePoint;
        _shooterCollider = FindShooterCollider(muzzlePoint);
        Transform player = playerPoint;

        if (player != null)
        {
            PlayerPosition = player.position;
        }

        ControlPoint = (MuzzlePosition + PlayerPosition) / 2f + new Vector2(0f, _curveHeight);
        _t = 0f;
        _isLaunching = true;
        _returnPoolTime = _returnPoolTimer;
        IncreaseDamage(damageIncreaseValue);
        ResetReflect();
    }

    protected abstract void Fire();

    /// <summary>
    /// 맞은 대상을 밀어낼 방향. 기본은 총구 → 목표 지점 (곡사 투사체 기준).
    /// 이동 방식이 다른 투사체(직진, 유도 등)는 실제 진행 방향으로 override.
    /// </summary>
    protected virtual Vector2 HitDirection => (PlayerPosition - MuzzlePosition).normalized;

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (_isReflected)
        {
            HandleReflectedHit(other);
            return;
        }

        Vector2 hitPoint = other.ClosestPoint(transform.position);

        if (other.TryGetComponent<IDamageable>(out IDamageable damageable))
        {
            var hitData = new HitData(
                _projectileDamage,
                _data.KnockbackForce,
                hitPoint,
                HitDirection,
                _defense,
                this,
                0,
                AttackName
            );

            damageable.TakeDamage(hitData);

            // 패링당해 반사됐으면 사라지지 않고 되돌아감
            if (_isReflected)
            {
                return;
            }
        }

        gameObject.SetActive(false);
    }

    public void IncreaseDamage(float percentage)
    {
        _projectileDamage = _data.ProjectileDamage * (1 + (percentage / 100f));
    }

    /// <summary>패링당하면 플레이어 투사체로 바뀌어 쏜 적 쪽으로 튕겨 나감</summary>
    public void OnParried(HitData hitData, Transform parrier)
    {
        if (_isReflected)
        {
            return;
        }

        _isReflected = true;
        _isLaunching = false;
        _returnPoolTime = _returnPoolTimer;

        // 반사 속도 / 데미지는 패링한 쪽(PlayerGuard) 설정을 따름. 없으면 원래 속도, 원래 데미지
        PlayerGuard guard = parrier != null ? parrier.GetComponent<PlayerGuard>() : null;
        _reflectSpeed = guard != null ? guard.ReflectSpeed : Speed;
        _projectileDamage *= guard != null ? guard.ReflectDamageMultiplier : 1f;
        _reflectDirection = CalculateReflectDirection(hitData);

        // 적에게 맞도록 플레이어 투사체 레이어로 변경
        SetLayerRecursively(transform, Constants.Layers.PlayerProjectile);
        UpdateReflectRotation();
    }

    private Vector2 CalculateReflectDirection(HitData hitData)
    {
        if (TryGetShooterDirection(out Vector2 toShooter))
        {
            return toShooter;
        }

        Vector2 back = -hitData.AttackDirection;
        return back.sqrMagnitude > 0.0001f ? back.normalized : Vector2.right;
    }

    /// <summary>쏜 적의 몸 중심 방향. 쏜 적이 없거나 꺼져 있으면 false</summary>
    private bool TryGetShooterDirection(out Vector2 direction)
    {
        direction = Vector2.zero;

        if (!IsReturningToShooter())
        {
            return false;
        }

        Vector2 toShooter = (Vector2)_shooterCollider.bounds.center - (Vector2)transform.position;
        if (toShooter.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        direction = toShooter.normalized;
        return true;
    }

    private bool IsReturningToShooter()
    {
        return _reflectToShooter && _shooterCollider != null && _shooterCollider.isActiveAndEnabled;
    }

    /// <summary>총구에서 위로 올라가며 쏜 적 본체(IDamageable)의 콜라이더를 찾음</summary>
    private static Collider2D FindShooterCollider(Transform muzzlePoint)
    {
        if (muzzlePoint == null)
        {
            return null;
        }

        IDamageable shooter = muzzlePoint.GetComponentInParent<IDamageable>();
        return shooter is Component component ? component.GetComponent<Collider2D>() : null;
    }

    // 2D 기준으로 쏜 적 콜라이더 영역 안인지 (z 무시)
    private bool IsInsideShooter()
    {
        Bounds bounds = _shooterCollider.bounds;
        Vector3 position = transform.position;
        position.z = bounds.center.z;
        return bounds.Contains(position);
    }

    private void MoveReflected()
    {
        // 쏜 적 몸에 도착했는데도 남아 있으면 (이미 쓰러진 적 등) 더 따라가지 않고 직진
        if (IsReturningToShooter() && IsInsideShooter())
        {
            _shooterCollider = null;
        }

        // 쏜 적이 움직여도 몸에 맞도록 매 프레임 방향을 다시 계산 (쏜 적이 사라지면 마지막 방향으로 직진)
        if (TryGetShooterDirection(out Vector2 toShooter))
        {
            _reflectDirection = toShooter;
            UpdateReflectRotation();
        }

        transform.position += (Vector3)(_reflectDirection * _reflectSpeed * Time.deltaTime);
    }

    private void UpdateReflectRotation()
    {
        // 적 투사체 스프라이트는 로컬 왼쪽(-right)이 앞쪽 → 진행 방향 + 180도
        float angle = Mathf.Atan2(_reflectDirection.y, _reflectDirection.x) * Mathf.Rad2Deg + 180f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void HandleReflectedHit(Collider2D other)
    {
        // 벽 / 바닥에 닿으면 사라짐 (쏜 적에게 되돌아가는 중이면 통과)
        if ((Constants.Layers.GroundAllMask & (1 << other.gameObject.layer)) != 0)
        {
            if (_ignoreGroundWhenReturning && IsReturningToShooter())
            {
                return;
            }

            gameObject.SetActive(false);
            return;
        }

        // 플레이어나 데미지를 받지 않는 트리거(탐지 범위 등)는 통과
        if (other.GetComponentInParent<Player>() != null)
        {
            return;
        }

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable == null)
        {
            return;
        }

        var hitData = new HitData(
            _projectileDamage,
            _data.KnockbackForce,
            other.ClosestPoint(transform.position),
            _reflectDirection,
            default,
            null,
            0,
            "반사된 투사체"
        );

        // 이미 쓰러진 적 등 데미지를 받지 않으면 통과
        if (damageable.TakeDamage(hitData))
        {
            gameObject.SetActive(false);
        }
    }

    private void ResetReflect()
    {
        if (_isReflected)
        {
            SetLayerRecursively(transform, _originalLayer);
        }

        _isReflected = false;
        _reflectDirection = Vector2.zero;
    }

    private static void SetLayerRecursively(Transform target, int layer)
    {
        target.gameObject.layer = layer;
        for (int i = 0; i < target.childCount; i++)
        {
            SetLayerRecursively(target.GetChild(i), layer);
        }
    }
}
