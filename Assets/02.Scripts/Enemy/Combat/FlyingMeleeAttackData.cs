using System;
using UnityEngine;

public enum FlyingMeleeAttackPattern
{
    Dive,
    Horizontal,
}

[Serializable]
public class FlyingMeleeAttackTiming
{
    [SerializeField, Min(0.01f)] private float _prepareTime = 0.85f;
    [SerializeField, Min(0.01f)] private float _strikeTime = 0.95f;
    [SerializeField, Min(0f)] private float _recoveryTime = 0.6f;

    public float PrepareTime => _prepareTime;
    public float StrikeTime => _strikeTime;
    public float RecoveryTime => _recoveryTime;
}

[CreateAssetMenu(fileName = "SO_InmyunjoAttackData", menuName = "Scriptable Objects/Enemy/Flying Melee Attack")]
public class FlyingMeleeAttackData : ScriptableObject
{
    [Header("패턴 선택")]
    [SerializeField, Range(0f, 1f)] private float _horizontalAttackChance = 0.2f;

    [Header("공격 시간")]
    [SerializeField] private FlyingMeleeAttackTiming _diveTiming = new FlyingMeleeAttackTiming();
    [SerializeField] private FlyingMeleeAttackTiming _horizontalTiming = new FlyingMeleeAttackTiming();

    [Header("준비 위치")]
    [SerializeField, Min(0f)] private float _prepareRetreatDistance = 0.6f;
    [SerializeField, Min(0.1f)] private float _approachDistance = 3f;
    [SerializeField, Min(0.1f)] private float _diveHeight = 2f;
    [SerializeField, Min(0.1f)] private float _positioningSpeed = 5f;
    [SerializeField, Min(0.1f)] private float _positioningTimeout = 2f;

    [Header("상공 대기")]
    [SerializeField, Min(0f)] private float _hoverTime = 0.9f;

    [Header("명중 감속")]
    [SerializeField, Range(0.1f, 1f)] private float _hitSpeedRatio = 0.6f;
    [SerializeField, Min(0f)] private float _hitSlowTime = 0.12f;

    public float PrepareRetreatDistance => _prepareRetreatDistance;
    public float ApproachDistance => _approachDistance;
    public float DiveHeight => _diveHeight;
    public float PositioningSpeed => _positioningSpeed;
    public float PositioningTimeout => _positioningTimeout;
    public float HoverTime => _hoverTime;
    public float HitSpeedRatio => _hitSpeedRatio;
    public float HitSlowTime => _hitSlowTime;

    public FlyingMeleeAttackPattern SelectPattern(float randomValue)
    {
        return randomValue < _horizontalAttackChance
            ? FlyingMeleeAttackPattern.Horizontal
            : FlyingMeleeAttackPattern.Dive;
    }

    public FlyingMeleeAttackTiming GetTiming(FlyingMeleeAttackPattern pattern)
    {
        return pattern == FlyingMeleeAttackPattern.Horizontal ? _horizontalTiming : _diveTiming;
    }
}
