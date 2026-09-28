using UnityEngine;

[CreateAssetMenu(fileName = "ProjectileData", menuName = "Scriptable Objects/ProjectileData")]
public class ProjectileData : ScriptableObject
{
    [Header("원거리 탄막")]

    [SerializeField] private float _projectileDamage;
    [SerializeField] private float _projectileSpeed;
    public float ProjectileDamage => _projectileDamage;
    public float ProjectileSpeed => _projectileSpeed;
}
