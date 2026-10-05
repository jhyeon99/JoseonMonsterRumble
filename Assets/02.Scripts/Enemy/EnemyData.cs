using UnityEngine;

[CreateAssetMenu(fileName = "EnemyData", menuName = "Scriptable Objects/EnemyData")]
public class EnemyData : ScriptableObject
{
    [Header("공통 능력치")]
    [SerializeField] private float _maxHp;
    [SerializeField] private float _moveSpeed;
    [SerializeField] private float _dieTime;
    public float MaxHp => _maxHp;
    public float MoveSpeed => _moveSpeed;
    public float DieTime => _dieTime;

    [Space(50)]
    [Header("근접 적 능력치")]
    [SerializeField]
    private float _meleeAttackDamage;
    [SerializeField] private float _chargeSpeed;
    [SerializeField] private float _knockbackForce;
    public float MeleeAttackDamage => _meleeAttackDamage;
    public float ChargeSpeed => _chargeSpeed;
    public float KnockbackForce => _knockbackForce;

    [Space(50)]
    [Header("공격 능력치")]
    [SerializeField]
    private float _attackDetectRange;
    [SerializeField] private float _attackPrepareTime;
    [SerializeField] private float _attackDurationTime;
    [SerializeField] private float _attackDelayTime;
    [SerializeField] private float _attackRecoveryTime;
    public float AttackDetectRange => _attackDetectRange;
    public float AttackPrepareTime => _attackPrepareTime;
    public float AttackDurationTime => _attackDurationTime;
    public float AttackDelayTime => _attackDelayTime;
    public float AttackRecoveryTime => _attackRecoveryTime;

    [Space(50)]
    [Header("버프 능력치")]
    [SerializeField]
    private float _attackBuffAmount;
    public float AttackBuffAmount => _attackBuffAmount;
}