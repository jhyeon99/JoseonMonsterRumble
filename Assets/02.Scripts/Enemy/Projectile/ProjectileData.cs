using UnityEngine;

[CreateAssetMenu(fileName = "ProjectileData", menuName = "Scriptable Objects/ProjectileData")]
public class ProjectileData : ScriptableObject
{
    [Header("원거리 탄막")]

    [SerializeField] private string _attackName = "원거리 공격";
    [SerializeField] private float _projectileDamage;
    [Tooltip("반사 피해를 별도로 지정합니다. 0이면 기존 PlayerGuard의 반사 피해 배율을 사용합니다.")]
    [SerializeField, Min(0f)] private float _reflectedDamage;
    [SerializeField] private float _projectileSpeed;
    [SerializeField] private float _knockbackForce;

    public string AttackName => _attackName;
    public float ProjectileDamage => _projectileDamage;
    public float ReflectedDamage => _reflectedDamage;
    public float ProjectileSpeed => _projectileSpeed;
    public float KnockbackForce => _knockbackForce;
}
