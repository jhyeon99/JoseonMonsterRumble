using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 공격 하나의 가드 / 패링 규칙. 공격 컴포넌트에 필드로 두고 인스펙터에서 조정.
/// 기본값(default)은 "패링 불가, 가드 가능"이라 값을 넣지 않은 기존 공격은 동작이 바뀌지 않음.
/// </summary>
[Serializable]
public struct AttackDefense
{
    [Tooltip("체크하면 가드 중 패링 타이밍에 맞았을 때 패링됨")]
    public bool Parryable;

    [Tooltip("체크하면 가드를 무시하고 그대로 맞음 (회피해야 하는 공격)")]
    public bool Unblockable;

    [Tooltip("이 공격만의 패링 판정 시간(초). 가드를 시작한 순간부터 이 시간 안에 맞으면 패링. 0이면 PlayerGuard의 기본값 사용")]
    [Min(0f)]
    public float ParryWindowOverride;

    [Tooltip("이 공격을 가드로 막았을 때 깎이는 가드 게이지 (패턴마다 다르게 설정)")]
    [Min(0f)]
    [FormerlySerializedAs("GuardDamage")]
    public float GuardGaugeCost;

    [Tooltip("이 공격을 가드 / 패링으로 막은 뒤 이 시간(초) 동안 다른 공격에 맞지 않음 (막은 직후 접촉 / 후속 판정에 바로 맞는 것 방지). 0이면 사용 안 함")]
    [Min(0f)]
    public float BlockInvincibleTime;

    [Tooltip("패링 불가 공격을 패링 타이밍에 막으면 가드 게이지 소모 없이 막기만 함 (패링 가능 공격에는 영향 없음)")]
    public bool FreeBlockOnParryTiming;
}
