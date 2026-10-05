using UnityEngine;

[System.Serializable]
public struct AttackData
{
    public float Damage;

    [Tooltip("판정 시작 지점 (공격 전체 길이 대비 비율 0~1, 전진 커브와 같은 기준)")]
    [Range(0f, 1f)] public float HitStartRatio;

    [Tooltip("판정 끝 지점 (비율 0~1). 시작보다 작거나 같으면 공격 전체 구간(0~1)에서 판정")]
    [Range(0f, 1f)] public float HitEndRatio;

    public float KnockbackForce;
    public Vector2 HitboxOffset;
    public Vector2 HitboxSize;
    public AnimationCurve ForwardDisplacementCurve;
    public float ComboStartNormalizedTime;
    public float ComboEndNormalizedTime;
}