using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Com.LuisPedroFonseca.ProCamera2D;

[InitializeOnLoad]
public static class PrologueIntroSetupEditor
{
    private const string ScenePath = "Assets/01.Scenes/Test_Ep0.unity";
    private const string ReportDirectory = @"C:\Users\qaz15\Documents\Codex\2026-10-07\problem-detected-while-opening-the-scene\work\prologue-live";
    private const string RequestPath = "Library/PrologueStorySetup.request";
    private const string VerifyPath = "Library/PrologueIntroVerify.request";
    private static bool _probing;
    private static double _probeStarted;
    private static double _completeAt;
    private static float _arrivalError;
    private static bool _physicsStayedEnabled;
    private static bool _groundedDuringIntro;
    private static int _testStep;
    private static double _stepStarted;
    private static bool _waitsForTransitionPoint;
    private static bool _followWorks;
    private static float _followError;
    private static Vector3 _followStart;
    private static bool _inspectionWorks;
    private static float _normalCameraSize;
    private static readonly List<string> Errors = new List<string>();
    private static readonly List<string> Phases = new List<string>();

    static PrologueIntroSetupEditor()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += PlayModeChanged;
        Application.logMessageReceived += CaptureLog;
        EditorApplication.delayCall += CheckRequest;
    }

    [MenuItem("Tools/Prologue/Apply intro setup")]
    public static void Configure()
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before applying scene settings.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                throw new InvalidOperationException("The active scene must be Test_Ep0.");

            var controller = ObjectsInScene<PrologueIntroController>(scene).Single();
            var serialized = new SerializedObject(controller);
            var player = serialized.FindProperty("_player").objectReferenceValue as Player;
            if (player == null) player = ObjectsInScene<Player>(scene).Single();
            var mainCamera = ObjectsInScene<Camera>(scene).Single(c => c.CompareTag("MainCamera"));
            var core = mainCamera.GetComponent<ProCamera2D>();
            if (core == null) core = Undo.AddComponent<ProCamera2D>(mainCamera.gameObject);
            var cinematic = mainCamera.GetComponent<ProCamera2DCinematics>();
            if (cinematic == null) cinematic = Undo.AddComponent<ProCamera2DCinematics>(mainCamera.gameObject);
            var spawn = serialized.FindProperty("_location2Spawn").objectReferenceValue as Transform;
            if (spawn == null) spawn = ObjectsInScene<Transform>(scene).Single(t => t.name == "SpawnPoint_02");
            var bg1 = serialized.FindProperty("_background1").objectReferenceValue as GameObject;
            var bg2 = serialized.FindProperty("_background2").objectReferenceValue as GameObject;
            if (bg1 == null || bg2 == null || bg1 == bg2)
                throw new InvalidOperationException("Background 1 and 2 must be distinct assigned objects.");
            var jige = serialized.FindProperty("_jige").objectReferenceValue as Transform;
            if (jige == null)
            {
                // The atlas contains many debris sprites; use the main broken frame.
                var frames = ObjectsInScene<SpriteRenderer>(scene)
                    .Where(r => r.sprite != null && r.sprite.name == "지게_0").ToArray();
                if (frames.Length != 1)
                    throw new InvalidOperationException("Assign the Jige field: expected one main 지게_0 sprite in this scene.");
                jige = frames[0].transform;
            }

            Undo.SetCurrentGroupName("Configure prologue intro");
            Undo.RecordObjects(new UnityEngine.Object[] { core, cinematic, controller, mainCamera }, "Configure prologue intro");
            core.enabled = true;
            core.GameCamera = mainCamera;
            core.Axis = MovementAxis.XY;
            core.UpdateType = UpdateType.LateUpdate;
            core.CenterTargetOnStart = false;
            core.FollowHorizontal = true;
            core.FollowVertical = true;
            core.HorizontalFollowSmoothness = 0.25f;
            core.VerticalFollowSmoothness = 0.35f;
            core.IsRelativeOffset = false;
            core.OffsetX = 0f;
            core.OffsetY = 1f;
            core.CameraTargets = new List<CameraTarget> {
                new CameraTarget { TargetTransform = player.transform, TargetInfluenceH = 1f, TargetInfluenceV = 1f }
            };
            var follow = mainCamera.GetComponent<CameraFollow>();
            if (follow != null) { Undo.RecordObject(follow, "Disable legacy follow"); follow.enabled = false; EditorUtility.SetDirty(follow); }

            var transition = GetOrCreate(scene, "Location2TransitionPoint", null,
                new Vector3(Mathf.Max(spawn.position.x - 8f, jige.position.x + 6f), player.transform.position.y, 0f));
            cinematic.enabled = true;
            cinematic.ProCamera2D = core;
            cinematic.UseLetterbox = false;
            cinematic.UseNumericBoundaries = false;
            cinematic.EndDuration = 0.5f;
            cinematic.EndEaseType = EaseType.EaseOut;
            cinematic.CinematicTargets = new List<CinematicTarget> {
                new CinematicTarget { TargetTransform = jige, EaseInDuration = 0.8f, HoldDuration = 2f, Zoom = 2f, EaseType = EaseType.EaseInOut }
            };

            serialized.Update();
            serialized.FindProperty("_player").objectReferenceValue = player;
            serialized.FindProperty("_camera").objectReferenceValue = core;
            serialized.FindProperty("_cinematics").objectReferenceValue = cinematic;
            serialized.FindProperty("_location2Spawn").objectReferenceValue = spawn;
            serialized.FindProperty("_location2TransitionPoint").objectReferenceValue = transition;
            serialized.FindProperty("_jige").objectReferenceValue = jige;
            serialized.FindProperty("_openingFade").floatValue = 1f;
            serialized.FindProperty("_fadeToBlack").floatValue = 0.5f;
            serialized.FindProperty("_revealLocation2").floatValue = 0.75f;
            serialized.FindProperty("_alignBackground2ToCamera").boolValue = true;
            var ui = scene.GetRootGameObjects().SingleOrDefault(go => go.name == "UI");
            serialized.FindProperty("_gameplayUi").objectReferenceValue = ui;
            serialized.ApplyModifiedProperties();
            controller.enabled = true;
            EditorUtility.SetDirty(core);
            EditorUtility.SetDirty(cinematic);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the configured scene.");
            Selection.activeGameObject = controller.gameObject;
            Write("setup-report.json", new SetupReport {
                success = true, scene = scene.path, jige = jige.position,
                player = player.transform.position, destination = spawn.position, transition = transition.position,
                background1 = bg1.name, background2 = bg2.name, ui = ui != null ? ui.name : "",
                targetCount = cinematic.CinematicTargets.Count,
                sourceBackground = SpriteBounds(bg1), destinationBackground = SpriteBounds(bg2)
            });
            Debug.Log("Prologue setup complete: camera targets, fade, destination and shared parallax reset are configured.");
        }
        catch (Exception error)
        {
            Write("setup-report.json", new SetupReport { success = false, error = error.ToString() });
            Debug.LogException(error);
        }
    }

    private static IEnumerable<T> ObjectsInScene<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<T>(true));

    private static Transform GetOrCreate(Scene scene, string name, Transform parent, Vector3 initialPosition)
    {
        var existing = ObjectsInScene<Transform>(scene).Where(t => t.name == name).ToArray();
        if (existing.Length > 1) throw new InvalidOperationException("Duplicate intro marker: " + name);
        if (existing.Length == 1) return existing[0];
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        Undo.RegisterCreatedObjectUndo(go, "Create intro camera marker");
        go.transform.SetParent(parent, true);
        go.transform.position = initialPosition;
        return go.transform;
    }

    private static void CheckRequest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || !File.Exists(RequestPath)) return;
        if (SceneManager.GetActiveScene().path != ScenePath) return;
        File.Delete(RequestPath);
        Configure();
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) { CheckRequest(); return; }
        if (!_probing) return;
        var scene = SceneManager.GetActiveScene();
        var controller = ObjectsInScene<PrologueIntroController>(scene).FirstOrDefault();
        if (controller == null) return;
        var runtimePlayer = ObjectsInScene<Player>(scene).FirstOrDefault();
        if (runtimePlayer != null)
        {
            _physicsStayedEnabled &= runtimePlayer.enabled && runtimePlayer.GetComponent<Rigidbody2D>().simulated;
            if (!controller.IsIntroComplete && runtimePlayer.IsGrounded) _groundedDuringIntro = true;
        }
        if (!Phases.Contains(controller.Phase)) Phases.Add(controller.Phase);
        var state = new SerializedObject(controller);
        var playerInput = runtimePlayer.GetComponent<PlayerInputHandler>();
        var rigidbody = runtimePlayer.GetComponent<Rigidbody2D>();
        var background1 = state.FindProperty("_background1").objectReferenceValue as GameObject;
        var background2 = state.FindProperty("_background2").objectReferenceValue as GameObject;
        var transition = state.FindProperty("_location2TransitionPoint").objectReferenceValue as Transform;
        var jige = state.FindProperty("_jige").objectReferenceValue as Transform;
        var core = state.FindProperty("_camera").objectReferenceValue as ProCamera2D;
        var now = EditorApplication.timeSinceStartup;
        if (controller.Phase == "Gameplay" && _testStep == 0)
        {
            _waitsForTransitionPoint = background1.activeSelf && !background2.activeSelf && playerInput.enabled;
            _followStart = core.transform.position;
            MoveForProbe(runtimePlayer, rigidbody.position + Vector2.right * 3f);
            _testStep = 1;
            _stepStarted = now;
        }
        else if (_testStep == 1 && now - _stepStarted > 1.5)
        {
            _waitsForTransitionPoint &= controller.Phase == "Gameplay" && background1.activeSelf && !background2.activeSelf;
            _followError = Vector2.Distance(core.transform.position,
                runtimePlayer.transform.position + new Vector3(core.OffsetX, core.OffsetY, 0f));
            _followWorks = Mathf.Abs(core.transform.position.x - _followStart.x) > 2f && _followError < 0.5f;
            MoveForProbe(runtimePlayer, new Vector2(jige.position.x - 3f, rigidbody.position.y));
            _testStep = 2;
            _stepStarted = now;
        }
        else if (_testStep == 2 && controller.Phase == "JigeDialogue")
        {
            _inspectionWorks = controller.CurrentSubtitle == "지게가 부서져 있다." && !playerInput.enabled &&
                core.GameCamera.orthographicSize < _normalCameraSize * 0.75f;
            _testStep = 3;
        }
        else if (_testStep == 3 && controller.Phase == "GameplayAfterJige")
        {
            _inspectionWorks &= controller.IsJigeInspected && playerInput.enabled && !core.GetComponent<ProCamera2DCinematics>().IsPlaying &&
                Mathf.Abs(core.GameCamera.orthographicSize - _normalCameraSize) < 0.1f;
            MoveForProbe(runtimePlayer, new Vector2(transition.position.x - 2f, rigidbody.position.y));
            _testStep = 4;
            _stepStarted = now;
        }
        else if (_testStep == 4 && now - _stepStarted > 0.75)
        {
            _waitsForTransitionPoint &= controller.Phase == "GameplayAfterJige" && background1.activeSelf && !background2.activeSelf;
            MoveForProbe(runtimePlayer, new Vector2(transition.position.x + 0.2f, rigidbody.position.y));
            _testStep = 5;
        }
        if (controller.IsTransitionComplete)
        {
            var so = new SerializedObject(controller);
            var player = so.FindProperty("_player").objectReferenceValue as Player;
            var spawn = so.FindProperty("_location2Spawn").objectReferenceValue as Transform;
            if (_completeAt == 0)
            {
                _completeAt = EditorApplication.timeSinceStartup;
                _arrivalError = Vector2.Distance(controller.LastTransferPosition, spawn.position);
            }
            if (EditorApplication.timeSinceStartup - _completeAt >= 1.0) FinishProbe(controller, null);
        }
        else if (EditorApplication.timeSinceStartup - _probeStarted > 40)
            FinishProbe(controller, "Prologue sequence did not complete within 40 seconds.");
    }

    // Only used during an explicitly requested verification run, never ordinary Play.
    private static void MoveForProbe(Player player, Vector2 position)
    {
        var body = player.GetComponent<Rigidbody2D>();
        body.position = position;
        body.linearVelocity = Vector2.zero;
        player.transform.position = new Vector3(position.x, position.y, player.transform.position.z);
        Physics2D.SyncTransforms();
    }

    private static void PlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode && SceneManager.GetActiveScene().path == ScenePath)
        {
            if (!File.Exists(VerifyPath)) return;
            File.Delete(VerifyPath);
            _probing = true; _probeStarted = EditorApplication.timeSinceStartup; _completeAt = 0;
            _physicsStayedEnabled = true; _groundedDuringIntro = false;
            _testStep = 0; _waitsForTransitionPoint = false; _followWorks = false;
            _inspectionWorks = false; _normalCameraSize = Camera.main.orthographicSize;
            Errors.Clear(); Phases.Clear();
        }
        if (change == PlayModeStateChange.ExitingPlayMode) _probing = false;
    }

    private static void CaptureLog(string message, string trace, LogType type)
    {
        if (_probing && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
            Errors.Add(message + "\n" + trace);
    }

    private static Bounds SpriteBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.sprite != null).ToArray();
        var bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(root.transform.position, Vector3.zero);
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    private static void FinishProbe(PrologueIntroController controller, string timeout)
    {
        _probing = false;
        var so = new SerializedObject(controller);
        var player = so.FindProperty("_player").objectReferenceValue as Player;
        var core = so.FindProperty("_camera").objectReferenceValue as ProCamera2D;
        var bg1 = so.FindProperty("_background1").objectReferenceValue as GameObject;
        var bg2 = so.FindProperty("_background2").objectReferenceValue as GameObject;
        var body = player.GetComponent<Rigidbody2D>();
        var input = player.GetComponent<PlayerInputHandler>();
        var bounds = SpriteBounds(bg2);
        var cam = core.GameCamera;
        var width = cam.orthographicSize * cam.aspect;
        var center = cam.transform.position;
        var covers = bounds.min.x <= center.x - width && bounds.max.x >= center.x + width &&
            bounds.min.y <= center.y - cam.orthographicSize && bounds.max.y >= center.y + cam.orthographicSize;
        var overlay = controller.GetComponentInChildren<CanvasGroup>();
        var errorList = Errors.ToList();
        if (timeout != null) errorList.Add(timeout);
        var report = new ProbeReport {
            complete = controller.IsTransitionComplete, phase = controller.Phase,
            sourceHidden = !bg1.activeSelf, destinationVisible = bg2.activeInHierarchy,
            controlsEnabled = player.enabled && input.enabled && body.simulated,
            physicsStayedEnabled = _physicsStayedEnabled, groundedDuringIntro = _groundedDuringIntro,
            groundedAfterTransition = player.IsGrounded,
            waitsForTransitionPoint = _waitsForTransitionPoint, cameraFollowWorks = _followWorks, followError = _followError,
            inspectionWorks = _inspectionWorks,
            backgroundCoversCamera = covers, arrivalError = _arrivalError,
            playerPosition = player.transform.position, cameraPosition = center, backgroundBounds = bounds,
            cameraError = Vector2.Distance(center, player.transform.position + new Vector3(core.OffsetX, core.OffsetY, 0)),
            fadeAlpha = overlay != null ? overlay.alpha : -1f,
            phases = Phases.ToArray(), errors = errorList.ToArray()
        };
        report.success = report.complete && report.sourceHidden && report.destinationVisible && report.controlsEnabled &&
            report.backgroundCoversCamera && report.arrivalError < 0.1f && report.fadeAlpha == 0 &&
            report.physicsStayedEnabled && report.groundedDuringIntro && report.groundedAfterTransition &&
            report.waitsForTransitionPoint && report.cameraFollowWorks && report.inspectionWorks && report.errors.Length == 0;
        Write("play-report.json", report);
        Debug.Log("Prologue play verification: " + (report.success ? "PASS" : "CHECK REPORT"));
    }

    private static void Write(string filename, object report)
    {
        Directory.CreateDirectory(ReportDirectory);
        File.WriteAllText(Path.Combine(ReportDirectory, filename), JsonUtility.ToJson(report, true));
    }

    [Serializable] private class SetupReport
    {
        public string configurationVersion = "story-v2";
        public bool success; public string error, scene, background1, background2, ui;
        public Vector3 player, destination, transition, jige;
        public int targetCount; public Bounds sourceBackground, destinationBackground;
    }
    [Serializable] private class ProbeReport
    {
        public bool success, complete, sourceHidden, destinationVisible, controlsEnabled, backgroundCoversCamera;
        public bool physicsStayedEnabled, groundedDuringIntro, groundedAfterTransition;
        public bool waitsForTransitionPoint, cameraFollowWorks;
        public bool inspectionWorks;
        public string phase; public string[] phases, errors;
        public float arrivalError, cameraError, fadeAlpha, followError;
        public Vector3 playerPosition, cameraPosition; public Bounds backgroundBounds;
    }
}
