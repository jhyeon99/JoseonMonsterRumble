using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 마무리 공격으로 적을 날려 보낼 때의 연출 값.
/// 실제로는 조금만 떠오르고, 회전하며 작아지고 흐려지는 것으로 "멀리 날아가는" 원근감을 표현.
/// PlayerFinisher 인스펙터에서 조정하고, 적의 LaunchedState가 사용.
/// </summary>
[Serializable]
public struct LaunchSettings
{
    [Tooltip("날아가는 시간 (초). 이 시간 동안 작아지고 흐려져서 사라짐")]
    [Min(0.1f)]
    public float Duration;

    [Tooltip("최대로 떠오르는 높이 (월드 단위). 화면 밖으로 나가지 않을 정도로")]
    [Min(0f)]
    public float Height;

    [Tooltip("최대로 좌우 이동하는 거리 (월드 단위). 화면 밖으로 나가지 않을 정도로")]
    public float Width;

    [Tooltip("시간(0~1)에 따른 높이 비율 (0~1). 처음에 빠르게 떠오르고 점점 느려지는 곡선이 멀어지는 느낌")]
    public AnimationCurve HeightCurve;

    [Tooltip("최대 회전 속도 (초당 각도). 실제 속도 = 이 값 x Spin Curve")]
    [FormerlySerializedAs("SpinSpeed")]
    public float MaxSpinSpeed;

    [Tooltip("시간(0~1)에 따른 회전 속도 배율 (0~1). 맞는 순간 가장 빠르게 돌다가 멀어지며 느려지는 곡선")]
    public AnimationCurve SpinCurve;

    [Tooltip("시간(0~1)에 따른 크기 배율. 1에서 0으로 줄어드는 곡선 (원근감의 핵심)")]
    public AnimationCurve ScaleCurve;

    [Tooltip("이 시점(0~1)부터 흐려지기 시작. 1이면 흐려지지 않음")]
    [Range(0f, 1f)]
    public float FadeStart;

    [Tooltip("체크하면 날아가는 동안 다른 오브젝트 뒤에 그려짐 (배경 쪽으로 멀어지는 느낌)")]
    public bool DrawBehind;

    public static LaunchSettings Default => new LaunchSettings
    {
        Duration = 1f,
        Height = 2.5f,
        HeightCurve = new AnimationCurve(new Keyframe(0f, 0f, 2.5f, 2.5f), new Keyframe(1f, 1f, 0f, 0f)),
        MaxSpinSpeed = 720f,
        SpinCurve = new AnimationCurve(new Keyframe(0f, 1f, 0f, 0f), new Keyframe(1f, 0.25f, -0.5f, 0f)),
        ScaleCurve = new AnimationCurve(new Keyframe(0f, 1f, -2.5f, -2.5f), new Keyframe(1f, 0f, 0f, 0f)),
        FadeStart = 0.6f,
        DrawBehind = true,
    };
}