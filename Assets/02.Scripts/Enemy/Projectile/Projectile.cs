using System;
using UnityEngine;

public abstract class Projectile : MonoBehaviour, IParryable
{
    [SerializeField] private ProjectileData _data;
    [SerializeField] private ProjectileType _type;
    [SerializeField] private float _curveHeight = 5f;
    [SerializeField] private float _returnPoolTimer = 5f;

    [Header("가드 / 패링")]
    [SerializeField]
    private AttackDefense _defense = new AttackDefense { Parryable = true, GuardGaugeCost = 25f };

    [Header("패링 반사 (속도 / 데미지 배율은 PlayerGuard에서 설정)")]
    [Tooltip("켜면 쏜 적의 총구 쪽으로 되돌아감. 끄거나 총구가 없으면 맞은 방향의 반대로 직진")]
    [SerializeField]
    private bool _reflectToShooter = true;

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

    // 반사 상태
    private int _originalLayer;
    private Transform _muzzleTransform;
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
                this
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
        if (_reflectToShooter && _muzzleTransform != null && _muzzleTransform.gameObject.activeInHierarchy)
        {
            Vector2 toShooter = (Vector2)_muzzleTransform.position - (Vector2)transform.position;
            if (toShooter.sqrMagnitude > 0.0001f)
            {
                return toShooter.normalized;
            }
        }

        Vector2 back = -hitData.AttackDirection;
        return back.sqrMagnitude > 0.0001f ? back.normalized : Vector2.right;
    }

    private void MoveReflected()
    {
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
        // 벽 / 바닥에 닿으면 사라짐
        if ((Constants.Layers.GroundAllMask & (1 << other.gameObject.layer)) != 0)
        {
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
            _reflectDirection
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
