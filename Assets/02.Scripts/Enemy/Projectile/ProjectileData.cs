using UnityEngine;

[CreateAssetMenu(fileName = "ProjectileData", menuName = "Scriptable Objects/ProjectileData")]
public class ProjectileData : ScriptableObject
{
    [Header("원거리 탄막")]

    [SerializeField] private string _attackName = "원거리 공격";
    [SerializeField] private float _projectileDamage;
    [SerializeField] private float _projectileSpeed;
    [SerializeField] private float _knockbackForce;

    public string AttackName => _attackName;
    public float ProjectileDamage => _projectileDamage;
    public float ProjectileSpeed => _projectileSpeed;
    public float KnockbackForce => _knockbackForce;
}
