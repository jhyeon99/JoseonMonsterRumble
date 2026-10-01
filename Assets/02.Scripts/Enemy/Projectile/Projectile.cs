using System;
using UnityEngine;

public abstract class Projectile : MonoBehaviour
{
    [SerializeField] private ProjectileData _data;
    [SerializeField] private ProjectileType _type;
    [SerializeField] private float _curveHeight = 5f;
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
    }

    private void Update()
    {
        Fire();
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
        IncreaseDamage(damageIncreaseValue);
    }

    protected abstract void Fire();

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("투사체가 부딪혔다.");
        gameObject.SetActive(false);
        if (!other.TryGetComponent<IDamageable>(out IDamageable damageable)) return;

        var hitData = new HitData(
            _projectileDamage,
            0f,
            Vector2.zero,
            Vector2.zero
        );

        damageable.TakeDamage(hitData);
        Debug.Log($"플레이어에게 원거리공격으로 {_data.ProjectileDamage}만큼 피해를 입힘.");
    }

    public void IncreaseDamage(float percentage)
    {
        _projectileDamage = _data.ProjectileDamage * (1 + (percentage / 100f));
    }
}
