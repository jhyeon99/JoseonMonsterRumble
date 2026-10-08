using System.Collections.Generic;
using System.IO;
using Com.LuisPedroFonseca.ProCamera2D;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class PlatformCameraZoomSetupEditor
{
    private const string RequestPath = "Library/PlatformCameraZoomSetup.request";
    private const string ReportPath = "C:/Users/qaz15/Documents/Codex/2026-10-07/problem-detected-while-opening-the-scene/work/ep1-camera/live-setup-report.json";
    private static double _nextCheck;

    static PlatformCameraZoomSetupEditor()
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

        if (EditorApplication.timeSinceStartup < _nextCheck)
            return;

        _nextCheck = EditorApplication.timeSinceStartup + 0.5;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/01.Scenes/Test_Ep1.unity")
            return;

        Camera mainCamera = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
            {
                if (camera.CompareTag("MainCamera") && camera.gameObject.activeInHierarchy)
                {
                    mainCamera = camera;
                    break;
                }
            }

            if (mainCamera != null)
                break;
        }

        ProCamera2D core = mainCamera != null ? mainCamera.GetComponent<ProCamera2D>() : null;
        if (core == null)
        {
            Finish("Main Camera에 ProCamera2D가 없습니다. 디스크의 Test_Ep1 씬을 다시 불러와야 합니다.", false, 0, 0);
            return;
        }

        MovingPlatformCameraZoom zoom = mainCamera.GetComponent<MovingPlatformCameraZoom>();
        bool added = zoom == null;
        if (added)
            zoom = Undo.AddComponent<MovingPlatformCameraZoom>(mainCamera.gameObject);

        Undo.RecordObject(zoom, "Connect platform camera zoom");
        zoom.ProCamera2D = core;
        zoom.enabled = true;

        PlayerMovement movement = null;
        foreach (CameraTarget target in core.CameraTargets)
        {
            if (target.TargetTransform == null)
                continue;

            movement = target.TargetTransform.GetComponentInParent<PlayerMovement>();
            if (movement != null)
                break;
        }

        if (movement != null)
        {
            var serializedZoom = new SerializedObject(zoom);
            serializedZoom.FindProperty("_playerMovement").objectReferenceValue = movement;
            serializedZoom.ApplyModifiedProperties();
        }

        EditorUtility.SetDirty(zoom);
        EditorSceneManager.MarkSceneDirty(scene);
        int movingCount = 0;
        int fallingCount = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            movingCount += root.GetComponentsInChildren<MovingPlatform>(true).Length;
            fallingCount += root.GetComponentsInChildren<FallingPlatform>(true).Length;
        }

        string message = added ? "열려 있는 씬에 줌 컴포넌트를 추가하고 플레이어를 연결했습니다." : "열려 있는 씬의 줌 컴포넌트와 플레이어 연결을 확인했습니다.";
        bool parallaxReady = true;
        if (File.ReadAllText(RequestPath).StartsWith("parallax"))
        {
            parallaxReady = ApplyParallaxBackdrop(mainCamera);
            message += parallaxReady ? " 카메라 하위 배경을 분리하고 반복 패럴렉스를 연결했습니다." : " 패럴렉스 배경 연결은 완료하지 못했습니다.";
        }

        if (File.ReadAllText(RequestPath).StartsWith("vertical-follow"))
        {
            Undo.RecordObject(core, "Allow downward camera follow");
            core.FollowVertical = true;
            ProCamera2DNumericBoundaries boundaries = mainCamera.GetComponent<ProCamera2DNumericBoundaries>();
            if (boundaries != null)
            {
                Undo.RecordObject(boundaries, "Remove camera bottom boundary");
                boundaries.UseBottomBoundary = false;
                EditorUtility.SetDirty(boundaries);
            }

            EditorUtility.SetDirty(core);
            EditorSceneManager.MarkSceneDirty(scene);
            message += " Y축 추적을 확인하고 아래쪽 카메라 경계를 해제했습니다.";
        }

        Finish(message, movement != null && core.enabled && parallaxReady, movingCount, fallingCount);
    }

    private static bool ApplyParallaxBackdrop(Camera mainCamera)
    {
        ParallaxManager manager = null;
        Scene scene = mainCamera.gameObject.scene;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (ParallaxManager candidate in root.GetComponentsInChildren<ParallaxManager>(true))
            {
                var settings = new SerializedObject(candidate);
                if (settings.FindProperty("_targetCamera").objectReferenceValue == mainCamera.transform)
                {
                    manager = candidate;
                    break;
                }
            }

            if (manager != null)
                break;
        }

        if (manager == null)
            return false;

        Transform stage = mainCamera.transform.parent;
        Transform group = stage != null ? stage.Find("Parallax_Backdrop") : null;
        var originals = new List<SpriteRenderer>();
        foreach (Transform child in mainCamera.transform)
        {
            if (child.TryGetComponent(out SpriteRenderer renderer) && renderer.sortingOrder == -100)
                originals.Add(renderer);
        }

        if (originals.Count == 0 && group != null)
        {
            foreach (Transform child in group)
            {
                if (!child.name.EndsWith("_Repeat") && child.TryGetComponent(out SpriteRenderer renderer))
                    originals.Add(renderer);
            }
        }

        if (originals.Count != 2)
            return false;

        originals.Sort((left, right) => left.transform.position.x.CompareTo(right.transform.position.x));
        if (group == null)
        {
            var groupObject = new GameObject("Parallax_Backdrop");
            Undo.RegisterCreatedObjectUndo(groupObject, "Create parallax backdrop");
            group = groupObject.transform;
            Undo.SetTransformParent(group, stage, "Parent parallax backdrop");
            group.position = mainCamera.transform.position;
        }

        float repeatWidth = 0f;
        foreach (SpriteRenderer image in originals)
            repeatWidth += image.sprite.rect.width / image.sprite.pixelsPerUnit * Mathf.Abs(image.transform.lossyScale.x);
        for (int i = 0; i < originals.Count; i++)
        {
            Transform image = originals[i].transform;
            Undo.SetTransformParent(image, group, "Detach backdrop from camera");

            Transform repeated = group.Find(image.name + "_Repeat");
            if (repeated == null)
            {
                GameObject repeatedObject = Object.Instantiate(image.gameObject, group);
                repeatedObject.name = image.name + "_Repeat";
                Undo.RegisterCreatedObjectUndo(repeatedObject, "Repeat parallax backdrop");
                repeated = repeatedObject.transform;
            }

            Undo.RecordObject(repeated, "Arrange repeated backdrop");
            repeated.localPosition = image.localPosition + new Vector3(repeatWidth, 0f, 0f);
            repeated.localScale = image.localScale;
        }

        Undo.RecordObject(manager, "Connect parallax backdrop");
        var serializedManager = new SerializedObject(manager);
        SerializedProperty layers = serializedManager.FindProperty("_layers");
        SerializedProperty layer = null;
        for (int i = 0; i < layers.arraySize; i++)
        {
            SerializedProperty candidate = layers.GetArrayElementAtIndex(i);
            if (candidate.FindPropertyRelative("_layerName").stringValue == "Platform Backdrop")
            {
                layer = candidate;
                break;
            }
        }

        if (layer == null)
        {
            int index = layers.arraySize;
            layers.arraySize++;
            layer = layers.GetArrayElementAtIndex(index);
        }

        layer.FindPropertyRelative("_layerName").stringValue = "Platform Backdrop";
        layer.FindPropertyRelative("_layer").objectReferenceValue = group;
        layer.FindPropertyRelative("_horizontalStrength").floatValue = 0.15f;
        layer.FindPropertyRelative("_verticalStrength").floatValue = 0.05f;
        layer.FindPropertyRelative("_loopHorizontally").boolValue = true;
        layer.FindPropertyRelative("_loopWidth").floatValue = repeatWidth;
        serializedManager.ApplyModifiedProperties();
        manager.enabled = true;
        manager.RebaseAtCurrentPosition();
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(scene);
        return true;
    }

    private static void Finish(string message, bool ready, int movingCount, int fallingCount)
    {
        string json = "{\"ready\":" + (ready ? "true" : "false") +
            ",\"movingPlatforms\":" + movingCount + ",\"fallingPlatforms\":" + fallingCount +
            ",\"message\":\"" + message + "\"}";
        File.WriteAllText(ReportPath, json);
        File.Delete(RequestPath);
        EditorApplication.update -= TryApply;
    }
}
