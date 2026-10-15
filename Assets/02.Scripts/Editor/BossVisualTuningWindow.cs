using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class BossVisualTuningWindow : EditorWindow
{
    private sealed class TuningPanel
    {
        public string Title;
        public int Category;
        public Object Target;
        public Editor Inspector;
        public bool IsExpanded;
    }

    private static readonly string[] Categories = { "색 보정 / Bloom / LUT", "전투 발광", "환경 / 조명 / 안개", "그림자", "UI / 카메라" };
    private readonly List<TuningPanel> _panels = new List<TuningPanel>();
    private Vector2 _scroll;
    private string _scenePath;
    private int _category;

    [MenuItem("Tools/Team06/보스방 연출 수치 조정")]
    public static void OpenWindow()
    {
        BossVisualTuningWindow window = GetWindow<BossVisualTuningWindow>("보스방 연출 조정");
        window.minSize = new Vector2(540f, 400f);
    }

    private void OnEnable()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorSceneManager.sceneOpened += HandleSceneOpened;
        UpdatePanels();
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorSceneManager.sceneOpened -= HandleSceneOpened;
        DestroyInspectors();
    }

    private void HandlePlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode)
            UpdatePanels();
    }

    private void HandleSceneOpened(Scene scene, OpenSceneMode mode) => UpdatePanels();

    private void DestroyInspectors()
    {
        foreach (TuningPanel panel in _panels)
            if (panel.Inspector != null) DestroyImmediate(panel.Inspector);
        _panels.Clear();
    }

    private Scene GetTargetScene()
    {
        Scene selected = SceneManager.GetSceneByPath(_scenePath ?? string.Empty);
        if (selected.IsValid() && selected.isLoaded) return selected;
        Scene active = SceneManager.GetActiveScene();
        if (active.path == "Assets/01.Scenes/Test_BossStage.unity") return active;
        Scene stage = SceneManager.GetSceneByPath("Assets/01.Scenes/Test_BossStage.unity");
        return stage.IsValid() && stage.isLoaded ? stage : active;
    }

    private void UpdatePanels()
    {
        DestroyInspectors();
        Scene scene = GetTargetScene();
        if (!scene.IsValid() || !scene.isLoaded) return;
        _scenePath = scene.path;
        Component[] components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true)).ToArray();
        foreach (Component component in components)
        {
            if (component == null) continue;
            if (component is Volume volume)
            {
                AddPanel("Volume 범위 / 적용 강도", volume, 0);
                VolumeProfile profile = volume.HasInstantiatedProfile() ? volume.profile : volume.sharedProfile;
                if (profile != null) AddPanel("색 보정 · Bloom · LUT · Tonemapping", profile, 0, true);
            }
            else if (component is CombatAttackGlow)
            {
                AddPanel("플레이어 공격 궤적 발광", component, 1, true);
            }
            else if (component is BossCombatLightController)
            {
                AddPanel("타격 / 패링 / 독 충돌 조명", component, 1, true);
            }
            else if (component is SpriteLightContactShadow)
            {
                AddPanel(component.name + " 접지 그림자", component, 3, true);
            }
            else if (component is Light2D && component.name != "CombatImpactLight" && component.name != "PoisonContactLight" && !component.name.Contains("SpriteLightShadow"))
            {
                AddPanel(component.name + " Light 2D", component, 2);
            }
            else if (component is Camera && component.GetComponent<UniversalAdditionalCameraData>() != null)
            {
                AddPanel(component.name + " SMAA / Post Processing", component, 4);
            }
            else if (component is Image)
            {
                AddPanel(component.name + " UI 색상", component, 4);
            }
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/10.Materials" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || material.name.Contains("Baseline")) continue;
            if (material.name.StartsWith("M_Combat")) AddPanel(GetMaterialTitle(material.name), material, 1);
            else if (material.name.StartsWith("M_Atmosphere") || material.name.StartsWith("M_BossRoom"))
                AddPanel(GetMaterialTitle(material.name), material, 2);
        }
        Repaint();
    }

    private void AddPanel(string title, Object target, int category, bool expanded = false)
    {
        if (_panels.Any(panel => panel.Target == target)) return;
        _panels.Add(new TuningPanel
        {
            Title = title,
            Category = category,
            Target = target,
            Inspector = Editor.CreateEditor(target),
            IsExpanded = expanded
        });
    }

    private static string GetMaterialTitle(string name)
    {
        switch (name)
        {
            case "M_CombatPlayerGlow": return "플레이어 발광 색 선택 / 문턱값";
            case "M_CombatHitGlow": return "타격 / 찍기 VFX 발광";
            case "M_CombatParryGlow": return "패링 VFX 발광";
            case "M_CombatTalismanGlow": return "부적 / 폭발 발광";
            case "M_CombatNeedleGlow": return "독침 발광";
            case "M_CombatSpitGlow": return "침뱉기 투사체 발광";
            case "M_CombatPoisonBeam": return "독분사 물줄기 발광 / 색상";
            case "M_CombatPoisonMouth": return "독분사 입구 발광 / 색상";
            case "M_CombatPoisonHead": return "독분사 머리 파티클 발광 / 색상";
            case "M_CombatPoisonResidual": return "독분사 잔여 파티클 발광 / 색상";
            case "M_CombatPoisonImpact": return "독분사 충돌 덩어리 발광 / 색상";
            case "M_BossRoomBackground": return "배경 대비 / 채도 / 안개색";
            case "M_BossRoomMist": return "안개 색 / 밀도 / 이동 속도";
            case "M_AtmosphereCharacterLit": return "플레이어 / 보스 소재 색 보정";
            case "M_AtmosphereSpriteLit": return "지형 / 잎 소재 색 보정";
            case "M_AtmosphereSpriteLitBlueKey": return "잎 소재 파란 배경 제거 / 색 보정";
            default: return name;
        }
    }

    private void OnGUI()
    {
        Scene scene = GetTargetScene();
        EditorGUILayout.LabelField("현재 연출 수치를 직접 편집합니다. 별도 설정 복사본은 만들지 않습니다.", EditorStyles.wordWrappedLabel);
        if (EditorGUILayout.DropdownButton(new GUIContent("대상 씬: " + scene.name), FocusType.Passive))
        {
            var menu = new GenericMenu();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene candidate = SceneManager.GetSceneAt(i);
                if (!candidate.isLoaded) continue;
                string path = candidate.path;
                menu.AddItem(new GUIContent(candidate.name), path == _scenePath, () => { _scenePath = path; UpdatePanels(); });
            }
            menu.ShowAsContext();
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("대상 다시 연결")) UpdatePanels();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                if (GUILayout.Button("씬과 조정 에셋 저장")) SaveTuningChanges();
        }
        EditorGUILayout.HelpBox(EditorApplication.isPlaying
            ? "플레이 중 Material 에셋 조정은 유지됩니다. 씬 조명·그림자·공격 강도는 정지하면 원래 값으로 돌아갑니다. 영구 조정은 정지 상태에서 하세요."
            : "Volume 항목 왼쪽 체크가 켜져야 수치가 적용됩니다. 발광이 과하면 전투 탭의 Emission Strength, 번짐이 과하면 Bloom Intensity를 낮추세요.", MessageType.Info);
        _category = GUILayout.SelectionGrid(_category, Categories, 3);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (TuningPanel panel in _panels)
        {
            if (panel.Category != _category || panel.Target == null || panel.Inspector == null) continue;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                panel.IsExpanded = EditorGUILayout.Foldout(panel.IsExpanded, panel.Title, true);
                if (!panel.IsExpanded) continue;
                EditorGUILayout.ObjectField("연결 대상", panel.Target, panel.Target.GetType(), true);
                EditorGUI.BeginChangeCheck();
                panel.Inspector.OnInspectorGUI();
                if (EditorGUI.EndChangeCheck())
                {
                    if (panel.Target is Material material) ApplyRuntimeMaterialSettings(material, scene);
                    else if (!EditorApplication.isPlaying && panel.Target is Component) EditorSceneManager.MarkSceneDirty(scene);
                    SceneView.RepaintAll();
                }
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private static void ApplyRuntimeMaterialSettings(Material source, Scene scene)
    {
        if (!EditorApplication.isPlaying) return;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material runtime in renderer.sharedMaterials)
                {
                    if (runtime == null || runtime == source || EditorUtility.IsPersistent(runtime) || runtime.shader != source.shader) continue;
                    if (runtime.name != source.name && !runtime.name.StartsWith(source.name + " ")) continue;
                    // 동작 중 계산되는 값은 독분사 코드가 계속 관리한다.
                    for (int i = 0; i < source.shader.GetPropertyCount(); i++)
                    {
                        string property = source.shader.GetPropertyName(i);
                        if (property == "_StreamLength" || property == "_EndImpact" || property == "_HasMouthConnection" || property == "_EndFrayLength") continue;
                        switch (source.shader.GetPropertyType(i))
                        {
                            case ShaderPropertyType.Color: runtime.SetColor(property, source.GetColor(property)); break;
                            case ShaderPropertyType.Vector: runtime.SetVector(property, source.GetVector(property)); break;
                            case ShaderPropertyType.Float:
                            case ShaderPropertyType.Range: runtime.SetFloat(property, source.GetFloat(property)); break;
                            case ShaderPropertyType.Int: runtime.SetInteger(property, source.GetInteger(property)); break;
                            case ShaderPropertyType.Texture:
                                runtime.SetTexture(property, source.GetTexture(property));
                                runtime.SetTextureScale(property, source.GetTextureScale(property));
                                runtime.SetTextureOffset(property, source.GetTextureOffset(property));
                                break;
                        }
                    }
                }
            }
        }
    }

    private void SaveTuningChanges()
    {
        foreach (TuningPanel panel in _panels)
        {
            if (panel.Target == null || !EditorUtility.IsPersistent(panel.Target)) continue;
            if (panel.Target is VolumeProfile profile)
                foreach (VolumeComponent component in profile.components) AssetDatabase.SaveAssetIfDirty(component);
            AssetDatabase.SaveAssetIfDirty(panel.Target);
        }
        Scene scene = GetTargetScene();
        if (scene.IsValid() && scene.isDirty) EditorSceneManager.SaveScene(scene);
    }
}
