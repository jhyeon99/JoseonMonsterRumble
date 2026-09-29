using System;
using UnityEngine;

public abstract class Projectile : MonoBehaviour
{
    [SerializeField] private ProjectileData _data;
    [SerializeField] private ProjectileType _type;
    [SerializeField] private float _curveHeight = 5f;
    private Transform _playerPosition;
    public ProjectileType Type => _type;
    protected float Speed { get; private set; }
    protected Vector2 ControlPoint { get; private set; }
    protected Vector2 MuzzlePosition { get; private set; }
    protected Vector2 PlayerPosition { get; private set; }
    protected float _t;
    protected bool _isLaunching = false;


    private void Awake()
    {
        Speed = _data.ProjectileSpeed;
    }

    private void Start()
    {
        _playerPosition = FindFirstObjectByType<Player>().transform;
    }

    private void Update()
    {
        Fire();
    }

    public void OnSpawn(Transform muzzlePoint)
    {
        MuzzlePosition = muzzlePoint.position;
        PlayerPosition = _playerPosition.position;
        ControlPoint = (MuzzlePosition + PlayerPosition) / 2f + new Vector2(0f, _curveHeight);
        _t = 0f;
        _isLaunching = true;
    }

    protected abstract void Fire();

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("투사체가 부딪혔다.");
        gameObject.SetActive(false);
        if (!other.TryGetComponent<IDamageable>(out IDamageable damageable)) return;

        HitData hitData = new HitData(
            _data.ProjectileDamage,
            0f,
            Vector2.zero,
            Vector2.zero
        );

        damageable.TakeDamage(hitData);
        Debug.Log($"플레이어에게 원거리공격으로 {_data.ProjectileDamage}만큼 피해를 입힘.");
    }
}
