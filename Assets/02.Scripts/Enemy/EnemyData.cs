using UnityEngine;

public enum EnemyAttackCycleMode
{
    RetreatRecovery = 0,
    StationaryCommitted = 1,
}

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

    [Header("걷기 애니메이션 이동")]
    [SerializeField] private bool _useWalkAnimationMotion;
    [SerializeField]
    private AnimationCurve _walkStartSpeedCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField]
    private AnimationCurve _walkEndSpeedCurve =
        AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    public bool UseWalkAnimationMotion => _useWalkAnimationMotion;

    public float CalculateWalkSpeedRatio(EnemyWalkPhase phase, float progress)
    {
        AnimationCurve curve = phase == EnemyWalkPhase.Start ? _walkStartSpeedCurve : _walkEndSpeedCurve;
        return Mathf.Clamp01(curve.Evaluate(Mathf.Clamp01(progress)));
    }

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
    [SerializeField] private EnemyAttackCycleMode _attackCycleMode;
    [SerializeField] private float _attackRecoverRange;
    [SerializeField] private float _attackPrepareTime;
    [SerializeField] private float _attackDurationTime;
    [SerializeField] private float _attackDelayTime;
    [SerializeField] private float _attackRecoveryTime;
    public float AttackDetectRange => _attackDetectRange;
    public float AttackRecoverRange => _attackRecoverRange;
    public float AttackPrepareTime => _attackPrepareTime;
    public float AttackDurationTime => _attackDurationTime;
    public float AttackDelayTime => _attackDelayTime;
    public float AttackRecoveryTime => _attackRecoveryTime;
    public bool HasStationaryAttackCycle => _attackCycleMode == EnemyAttackCycleMode.StationaryCommitted;
    [Space(50)]
    [Header("원거리 적 이동 제한 범위")]

    [Range(0f, 90f)]
    [SerializeField]
    private float _attackAngle;
    public float AttackAngle => _attackAngle;

    [Space(50)]
    [Header("버프 능력치")]

    [SerializeField]
    private float _attackBuffAmount;
    public float AttackBuffAmount => _attackBuffAmount;
    [SerializeField] private float _moveToEnemySpeed;
    public float MoveToEnemySpeed => _moveToEnemySpeed;
}
