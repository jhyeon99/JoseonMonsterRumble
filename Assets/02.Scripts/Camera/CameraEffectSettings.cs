using System;
using UnityEngine;

/// <summary>카메라 흔들림 설정 (점점 약해짐)</summary>
[Serializable]
public struct CameraShakeSettings
{
    [Tooltip("흔들리는 시간 (실제 초). 0이면 사용 안 함")]
    [Min(0f)]
    public float Duration;

    [Tooltip("처음 흔들림 세기 (월드 거리). 시간이 지나며 0으로 줄어듦")]
    [Min(0f)]
    public float Magnitude;

    public CameraShakeSettings(float duration, float magnitude)
    {
        Duration = duration;
        Magnitude = magnitude;
    }
}

/// <summary>한 방향으로 카메라를 밀었다가 되돌리기</summary>
[Serializable]
public struct CameraKickSettings
{
    [Tooltip("밀리는 거리 (월드 거리). 0이면 사용 안 함")]
    [Min(0f)]
    public float Distance;

    [Tooltip("밀렸다가 돌아오기까지 전체 시간 (실제 초)")]
    [Min(0.01f)]
    public float Duration;

    [Tooltip("전체 시간 중 밀리는 구간 비율. 나머지 구간 동안 천천히 돌아옴")]
    [Range(0.05f, 0.9f)]
    public float PushRatio;

    public CameraKickSettings(float distance, float duration, float pushRatio = 0.2f)
    {
        Distance = distance;
        Duration = duration;
        PushRatio = pushRatio;
    }
}

/// <summary>줌 인 (+ 지정 위치 쪽으로 카메라 이동) 후 원래대로</summary>
[Serializable]
public struct CameraZoomSettings
{
    [Tooltip("줌 정도. 0.12면 화면이 12% 확대 (음수면 줌 아웃). 0이면 사용 안 함")]
    [Range(-0.5f, 0.5f)]
    public float Amount;

    [Tooltip("줌 인 되는 시간 (실제 초)")]
    [Min(0f)]
    public float InTime;

    [Tooltip("줌 상태로 유지하는 시간 (실제 초)")]
    [Min(0f)]
    public float HoldTime;

    [Tooltip("원래대로 돌아오는 시간 (실제 초)")]
    [Min(0f)]
    public float OutTime;

    [Tooltip("지정 위치(타격 지점) 쪽으로 카메라를 옮기는 비율. 0이면 이동 없이 줌만")]
    [Range(0f, 1f)]
    public float FocusStrength;

    [Tooltip("지정 위치 쪽으로 이동하는 최대 거리 (월드 거리)")]
    [Min(0f)]
    public float MaxFocusDistance;

    public CameraZoomSettings(
        float amount,
        float inTime,
        float holdTime,
        float outTime,
        float focusStrength = 0f,
        float maxFocusDistance = 2f
    )
    {
        Amount = amount;
        InTime = inTime;
        HoldTime = holdTime;
        OutTime = outTime;
        FocusStrength = focusStrength;
        MaxFocusDistance = maxFocusDistance;
    }

    public float TotalTime => InTime + HoldTime + OutTime;
}
