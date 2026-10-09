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
    [SerializeField, Min(0f)] private float _attackRangeJitter;
    public float AttackDetectRange => _attackDetectRange;
    public float AttackRecoverRange => _attackRecoverRange;
    public float AttackPrepareTime => _attackPrepareTime;
    public float AttackDurationTime => _attackDurationTime;
    public float AttackDelayTime => _attackDelayTime;
    public float AttackRecoveryTime => _attackRecoveryTime;
    public float AttackRangeJitter => _attackRangeJitter;
    public bool HasStationaryAttackCycle => _attackCycleMode == EnemyAttackCycleMode.StationaryCommitted;

    public float CalculateRandomAttackDetectRange()
    {
        if (_attackRangeJitter <= 0f)
        {
            return _attackDetectRange;
        }

        return Mathf.Max(2.0f, _attackDetectRange + Random.Range(-_attackRangeJitter, _attackRangeJitter));
    }

    [Header("지상 원거리 안전거리")]
    [SerializeField, Min(0f)] private float _rangedFleeStartDistance;
    [SerializeField, Min(0f)] private float _rangedFleeEndDistance;
    public float RangedFleeStartDistance => _rangedFleeStartDistance;
    public float RangedFleeEndDistance => Mathf.Max(_rangedFleeStartDistance, _rangedFleeEndDistance);

    [Header("지상 원거리 공격 전 짧은 대기")]
    [SerializeField, Range(0f, 1f)] private float _rangedAttackIdleChance;
    [SerializeField, Min(0f)] private float _rangedAttackIdleDuration;
    public float RangedAttackIdleChance => _rangedAttackIdleChance;
    public float RangedAttackIdleDuration => _rangedAttackIdleDuration;

    [Header("원거리 적 도발")]
    [SerializeField, Range(0f, 1f)] private float _provokeChance;
    public float ProvokeChance => _provokeChance;
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
