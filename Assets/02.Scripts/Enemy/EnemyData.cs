using UnityEngine;

[CreateAssetMenu(fileName = "EnemyData", menuName = "Scriptable Objects/EnemyData")]
public class EnemyData : ScriptableObject
{
    [Header("공통 능력치")]

    [SerializeField] private float _maxHp;
    [SerializeField] private float _moveSpeed;
    [SerializeField] private float _detectRange;
    public float MaxHp => _maxHp;
    public float MoveSpeed => _moveSpeed;
    public float DetectRange => _detectRange;

    [Space(50)]
    [Header("공격 능력치")]

    [SerializeField]
    private float _attackDamage;
    [SerializeField] private float _chargeSpeed;
    [SerializeField] private float _attackDetectRange;
    [SerializeField] private float _attackPrepareTime;
    [SerializeField] private float _attackDurationTime;
    [SerializeField] private float _attackRecoveryTime;
    public float AttackDamage => _attackDamage;
    public float ChargeSpeed => _chargeSpeed;
    public float AttackDetectRange => _attackDetectRange;
    public float AttackPrepareTime => _attackPrepareTime;
    public float AttackDurationTime => _attackDurationTime;
    public float AttackRecoveryTime => _attackRecoveryTime;

    [Space(50)]
    [Header("버프 능력치")]

    [SerializeField]
    private float _attackBuffAmount;
    [SerializeField] private float _buffRange;
    public float AttackBuffAmount => _attackBuffAmount;
    public float BuffRange => _buffRange;
}
