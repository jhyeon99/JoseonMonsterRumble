using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class Ep0CombatTutorialSetupEditor
{
    private const string RequestPath = "Library/Ep0CombatTutorialSetup.request";
    private const string PrefabPath = "Assets/03.Prefabs/PF_Ep0CombatTutorial.prefab";
    private const string ReportPath = "Library/Ep0CombatTutorialSetup.report";

    static Ep0CombatTutorialSetupEditor()
    {
        EditorApplication.update += TrySetup;
    }

    private static void TrySetup()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetSceneByPath("Assets/01.Scenes/Test_Ep0.unity");
        if (!scene.IsValid() || !scene.isLoaded) return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponent<Ep0CombatTutorial>() == null) continue;
            File.WriteAllText(ReportPath, "Existing tutorial found. Scene was not saved.");
            File.Delete(RequestPath);
            return;
        }

        Transform spawn = null;
        Player player = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == "SpawnPoint_02") spawn = child;
            if (player == null) player = root.GetComponentInChildren<Player>(true);
        }
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null || spawn == null || player == null) return;

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        Undo.RegisterCreatedObjectUndo(instance, "Add EP0 combat tutorial");
        instance.transform.position = spawn.position + Vector3.right * 8f;
        Ep0CombatTutorial tutorial = instance.GetComponent<Ep0CombatTutorial>();
        var serialized = new SerializedObject(tutorial);
        serialized.FindProperty("_player").objectReferenceValue = player;
        serialized.FindProperty("_startPoint").objectReferenceValue = spawn;
        serialized.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(scene);
        File.WriteAllText(ReportPath, "Tutorial added 8 units ahead of SpawnPoint_02. Scene was not saved; existing unsaved edits were retained.");
        File.Delete(RequestPath);
    }
}
