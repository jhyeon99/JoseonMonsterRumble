using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class PrologueDialoguePanelSetupEditor
{
    private const string RequestPath = "Library/PrologueDialoguePanelSetup.request";
    private const string SpritePath = "Assets/05.Art/UI/Dialogue/DialoguePanel_Sumukhwa.png";
    private const string ReportPath = "C:/Users/qaz15/Documents/Codex/2026-10-07/problem-detected-while-opening-the-scene/work/prologue-live/dialogue-panel-report.json";
    private static double _nextCheck;

    static PrologueDialoguePanelSetupEditor()
    {
        if (File.Exists(RequestPath)) EditorApplication.update += TryApply;
    }

    private static void TryApply()
    {
        if (!File.Exists(RequestPath)) { EditorApplication.update -= TryApply; return; }
        if (EditorApplication.timeSinceStartup < _nextCheck) return;
        _nextCheck = EditorApplication.timeSinceStartup + 1;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        Sprite sprite = null;
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(SpritePath))
        {
            if (asset is Sprite candidate) { sprite = candidate; break; }
        }
        if (sprite == null) return;
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long fileId);
        var report = new Report { Imported = true, Guid = guid, FileId = fileId, Rect = sprite.rect, Border = sprite.border };
        Scene scene = SceneManager.GetSceneByPath("Assets/01.Scenes/Test_Ep0.unity");
        if (scene.IsValid() && scene.isLoaded && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (PrologueIntroController controller in root.GetComponentsInChildren<PrologueIntroController>(true))
                {
                    var serialized = new SerializedObject(controller);
                    serialized.FindProperty("_dialoguePanelSprite").objectReferenceValue = sprite;
                    serialized.FindProperty("_dialogueTextColor").colorValue = new Color(0.08f, 0.075f, 0.07f, 1f);
                    serialized.ApplyModifiedProperties();
                    EditorUtility.SetDirty(controller);
                    EditorSceneManager.MarkSceneDirty(scene);
                    report.AppliedToOpenScene = true;
                }
            }
        }
        File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
        if (!report.AppliedToOpenScene) return;
        File.Delete(RequestPath);
        EditorApplication.update -= TryApply;
    }

    [Serializable]
    private sealed class Report
    {
        public bool Imported, AppliedToOpenScene;
        public string Guid;
        public long FileId;
        public Rect Rect;
        public Vector4 Border;
    }
}
