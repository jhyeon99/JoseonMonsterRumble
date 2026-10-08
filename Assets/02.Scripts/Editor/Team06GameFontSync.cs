using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Team06GameFontSync
{
    private const string RequestPath = "Library/Team06GameFontSync.request";
    private const string ReportPath = "Library/Team06GameFontSync-report.txt";
    private const string FontPath = "Assets/05.Art/Fonts/Team06/Team06-InkKR-Regular.ttf";

    static Team06GameFontSync()
    {
        if (File.Exists(RequestPath)) EditorApplication.update += ApplyRequestedFonts;
    }

    private static void ApplyRequestedFonts()
    {
        if (!File.Exists(RequestPath)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (font == null) return;

        int count = SyncLoadedScenes(font);
        File.WriteAllText(ReportPath, $"Imported=True; LoadedDialogueControllers={count}; Font={FontPath}");
        File.Delete(RequestPath);
        EditorApplication.update -= ApplyRequestedFonts;
    }

    [MenuItem("Tools/Team06/Apply Imported Dialogue Font")]
    private static void ApplyImportedDialogueFont()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (font == null) return;
        SyncLoadedScenes(font);
    }

    private static int SyncLoadedScenes(Font font)
    {
        int count = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded || !scene.path.StartsWith("Assets/01.Scenes/")) continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (PrologueIntroController controller in
                         root.GetComponentsInChildren<PrologueIntroController>(true))
                {
                    var serialized = new SerializedObject(controller);
                    SerializedProperty property = serialized.FindProperty("_subtitleFont");
                    if (property == null) continue;
                    if (property.objectReferenceValue != font)
                    {
                        Undo.RecordObject(controller, "Apply Team06 dialogue font");
                        property.objectReferenceValue = font;
                        serialized.ApplyModifiedProperties();
                    }

                    count++;
                }
            }
        }

        return count;
    }
}
