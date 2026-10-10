using UnityEngine;

/// <summary>스테이지 점프 키를 공용 입력 버퍼 처리와 분리한다.</summary>
public static class StageJumpInput
{
    public static bool WasPressedThisFrame =>
        Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.Space);

    public static bool IsHeld =>
        Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.Space);
}
