using System;
using UnityEngine;

public abstract class Projectile : MonoBehaviour
{
    [SerializeField] private ProjectileData _data;
    [SerializeField] private ProjectileType _type;
    [SerializeField] private float _curveHeight = 5f;
    [SerializeField] private float _returnPoolTimer = 5f;
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


    private void Awake()
    {
        Speed = _data.ProjectileSpeed;
        _projectileDamage = _data.ProjectileDamage;
        _returnPoolTime = _returnPoolTimer;
    }

    private void Update()
    {
        Fire();
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

    public void OnSpawn(Transform muzzlePoint, Transform playerPoint, float damageIncreaseValue)
    {
        MuzzlePosition = muzzlePoint.position;
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
    }

    protected abstract void Fire();

    /// <summary>
    /// 맞은 대상을 밀어낼 방향. 기본은 총구 → 목표 지점 (곡사 투사체 기준).
    /// 이동 방식이 다른 투사체(직진, 유도 등)는 실제 진행 방향으로 override.
    /// </summary>
    protected virtual Vector2 HitDirection => (PlayerPosition - MuzzlePosition).normalized;

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        Vector2 hitPoint = other.ClosestPoint(transform.position);

        if (other.TryGetComponent<IDamageable>(out IDamageable damageable))
        {
            var hitData = new HitData(
                _projectileDamage,
                _data.KnockbackForce,
                hitPoint,
                HitDirection
            );

            damageable.TakeDamage(hitData);
        }

        gameObject.SetActive(false);
    }


    public void IncreaseDamage(float percentage)
    {
        _projectileDamage = _data.ProjectileDamage * (1 + (percentage / 100f));
    }
}