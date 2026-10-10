using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class TutorialJumpLedgeSetupEditor
{
    private const string RequestPath = "Library/TutorialJumpLedgeCleanup.request";
    private const string PrefabPath = "Assets/03.Prefabs/Platform/Tutorial_JumpLedge.prefab";
    private const string ReportPath = "C:/Users/qaz15/Documents/Codex/2026-10-07/problem-detected-while-opening-the-scene/work/prologue-live/jump-ledge-cleanup-report.txt";

    static TutorialJumpLedgeSetupEditor()
    {
        if (File.Exists(RequestPath)) EditorApplication.update += RemoveCreatedInstance;
    }

    private static void RemoveCreatedInstance()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetSceneByPath("Assets/01.Scenes/Test_Ep0.unity");
        if (!scene.IsValid() || !scene.isLoaded) return;
        int removed = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != "Tutorial_JumpLedge") continue;
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) != PrefabPath) continue;
            if (Vector3.Distance(root.transform.position, new Vector3(92f, -3.1640635f, 0f)) > 0.001f) continue;
            Undo.DestroyObjectImmediate(root);
            removed++;
        }
        if (removed > 0) EditorSceneManager.MarkSceneDirty(scene);
        File.WriteAllText(ReportPath, "Removed automatic scene instance: " + removed + ". Prefab retained. Scene was not saved.");
        File.Delete(RequestPath);
        EditorApplication.update -= RemoveCreatedInstance;
    }
}
