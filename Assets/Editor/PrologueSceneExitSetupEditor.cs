using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class PrologueSceneExitSetupEditor
{
    private const string RequestPath = "Library/PrologueSceneExitSetup.request";
    private const string ReportPath = "C:/Users/qaz15/Documents/Codex/2026-10-07/problem-detected-while-opening-the-scene/work/prologue-live/episode-exit-report.json";
    private static double _nextCheck;

    static PrologueSceneExitSetupEditor()
    {
        if (File.Exists(RequestPath))
            EditorApplication.update += TryApply;
    }

    private static void TryApply()
    {
        if (!File.Exists(RequestPath))
        {
            EditorApplication.update -= TryApply;
            return;
        }
        if (EditorApplication.timeSinceStartup < _nextCheck) return;
        _nextCheck = EditorApplication.timeSinceStartup + 1;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/01.Scenes/Test_Ep0.unity") return;
        PrologueIntroController controller = null;
        Transform exit = null;
        int exitCount = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (PrologueIntroController candidate in root.GetComponentsInChildren<PrologueIntroController>(true))
                controller = candidate;
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name != "Exit_To_Test_Ep1") continue;
                exit = candidate;
                exitCount++;
            }
        }
        if (controller == null || exitCount != 1) return;

        var serialized = new SerializedObject(controller);
        SerializedProperty point = serialized.FindProperty("_nextSceneExitPoint");
        if (point == null) return;
        point.objectReferenceValue = exit;
        serialized.FindProperty("_nextSceneName").stringValue = "Test_Ep1";
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        File.WriteAllText(ReportPath, JsonUtility.ToJson(new Report { Ready = true, ExitName = exit.name, Position = exit.position }));
        File.Delete(RequestPath);
        EditorApplication.update -= TryApply;
    }

    [System.Serializable]
    private sealed class Report
    {
        public bool Ready;
        public string ExitName;
        public Vector3 Position;
    }
}
