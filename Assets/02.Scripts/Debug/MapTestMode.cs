using UnityEngine;

/// <summary>에디터에서 F2로 몬스터 공격과 플레이어 피격을 끄는 맵 테스트 모드.</summary>
[DefaultExecutionOrder(-100)]
public sealed class MapTestMode : MonoBehaviour
{
#if UNITY_EDITOR
    public static bool IsEnabled { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetMode()
    {
        IsEnabled = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateController()
    {
        if (FindFirstObjectByType<MapTestMode>() != null) return;

        var controller = new GameObject("Map Test Mode (F2)");
        DontDestroyOnLoad(controller);
        controller.AddComponent<MapTestMode>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F2))
        {
            IsEnabled = !IsEnabled;
            Debug.Log(IsEnabled ? "맵 테스트 켜짐: 몬스터 공격과 플레이어 피격을 막습니다. (F2로 해제)"
                : "맵 테스트 꺼짐: 몬스터 공격과 플레이어 피격을 복구합니다.");
        }
    }

    private void OnGUI()
    {
        if (IsEnabled)
        {
            GUI.Box(new Rect(12f, 12f, 340f, 36f), "맵 테스트: 몬스터 공격 꺼짐 · F2로 해제");
        }
    }
#else
    public static bool IsEnabled => false;
#endif
}
