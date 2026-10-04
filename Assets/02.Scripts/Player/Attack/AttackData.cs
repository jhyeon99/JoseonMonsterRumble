using UnityEngine;

[System.Serializable]
public struct AttackData
{
    public float Damage;
    public float ActiveDuration;
    public float KnockbackForce;
    public Vector2 HitboxOffset;
    public Vector2 HitboxSize;
    public float ForwardDelay;
    public float ForwardSpeed;
    public float ForwardDuration;
    public AnimationCurve ForwardDisplacementCurve;
    public float ReadyDurationRatio;
    public float ComboStartNormalizedTime;
    public float ComboEndNormalizedTime;
}