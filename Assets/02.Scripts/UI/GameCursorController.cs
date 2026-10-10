using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameCursorController
{
    private const string TitleSceneName = "Test_TitleScene";
    private const string CursorResourcePath = "JoseonInkCursor";
    private static readonly Vector2 Hotspot = new Vector2(2f, 2f);

    private static Texture2D _cursorTexture;
    private static bool _pauseVisible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Application.quitting -= RestoreDefault;
        Application.quitting += RestoreDefault;

        _pauseVisible = false;
        _cursorTexture = Resources.Load<Texture2D>(CursorResourcePath);
        if (_cursorTexture == null)
        {
            Debug.LogWarning($"커서 이미지를 찾을 수 없습니다: Resources/{CursorResourcePath}");
        }
    }

    public static void SetPauseVisible(bool visible)
    {
        _pauseVisible = visible;
        Apply(SceneManager.GetActiveScene().name);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _pauseVisible = false;
        Apply(scene.name);
    }

    private static void Apply(string sceneName)
    {
        bool show = sceneName == TitleSceneName || _pauseVisible;
        Cursor.SetCursor(show ? _cursorTexture : null, show ? Hotspot : Vector2.zero, CursorMode.Auto);
        Cursor.visible = show;
    }

    private static void RestoreDefault()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        Cursor.visible = true;
    }
}
