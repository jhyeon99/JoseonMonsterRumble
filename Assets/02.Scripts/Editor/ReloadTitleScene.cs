#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ReloadTitleScene
{
    [MenuItem("Tools/Reload Title Scene From Disk")]
    public static void Reload()
    {
        EditorSceneManager.OpenScene("Assets/01.Scenes/Test_TitleScene.unity");
    }
}
#endif
