using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

[CustomEditor(typeof(EnemyMoveArea))]
public sealed class EnemyMoveAreaEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "범위 편집 버튼을 누른 뒤 씬의 변 핸들을 드래그해 크기를 조절하세요. 가운데 핸들은 이동·감지·추적 범위의 공통 중심을 옮깁니다.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            DrawEditButton(EnemyMoveAreaSceneTool.AreaEditMode.Movement, "이동 범위");
            DrawEditButton(EnemyMoveAreaSceneTool.AreaEditMode.Detection, "플레이어 감지 범위");
            DrawEditButton(EnemyMoveAreaSceneTool.AreaEditMode.Chase, "플레이어 추적 범위");
        }

        if (ToolManager.activeToolType == typeof(EnemyMoveAreaSceneTool))
        {
            if (EnemyMoveAreaSceneTool.EditMode == EnemyMoveAreaSceneTool.AreaEditMode.Detection)
                EditorGUILayout.HelpBox("하늘색 범위를 조절하면 이동 범위와 별도로 감지 크기가 저장됩니다.", MessageType.Info);
            else if (EnemyMoveAreaSceneTool.EditMode == EnemyMoveAreaSceneTool.AreaEditMode.Chase)
                EditorGUILayout.HelpBox("노란 범위를 벗어나면 추적을 멈춥니다. 변을 드래그하면 사방의 추적 여유 폭이 함께 바뀝니다.", MessageType.Info);
        }
    }

    private static void DrawEditButton(EnemyMoveAreaSceneTool.AreaEditMode mode, string label)
    {
        bool isActive = ToolManager.activeToolType == typeof(EnemyMoveAreaSceneTool);
        bool isEditing = isActive && EnemyMoveAreaSceneTool.EditMode == mode;
        if (!GUILayout.Button(isEditing ? label + " 편집 마치기" : "씬에서 " + label + " 편집"))
            return;

        if (isEditing)
            ToolManager.RestorePreviousTool();
        else
        {
            EnemyMoveAreaSceneTool.EditMode = mode;
            if (!isActive)
                ToolManager.SetActiveTool<EnemyMoveAreaSceneTool>();
        }

        SceneView.RepaintAll();
    }
}

[EditorTool("적 범위 편집", typeof(EnemyMoveArea))]
public sealed class EnemyMoveAreaSceneTool : EditorTool
{
    internal enum AreaEditMode { Movement, Detection, Chase }
    internal static AreaEditMode EditMode { get; set; }

    private readonly BoxBoundsHandle _boundsHandle = new BoxBoundsHandle
    {
        axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Y
    };

    private GUIContent _toolbarIcon;

    public override GUIContent toolbarIcon
    {
        get
        {
            if (_toolbarIcon == null)
            {
                _toolbarIcon = new GUIContent(EditorGUIUtility.IconContent("EditCollider"))
                {
                    tooltip = "씬에서 적 이동·감지·추적 범위 편집"
                };
            }

            return _toolbarIcon;
        }
    }

    public override void OnToolGUI(EditorWindow window)
    {
        if (!(window is SceneView) || !(target is EnemyMoveArea area) ||
            EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        // All runtime bounds use world X/Y units and the same CenterOffset.
        Vector3 origin = area.transform.position;
        Vector3 center = origin + new Vector3(area.CenterOffset.x, area.CenterOffset.y, 0f);
        Color handleColor = new Color(area.AreaColor.r, area.AreaColor.g, area.AreaColor.b, 1f);
        Vector2 size = area.AreaSize;
        float margin = 0f;
        string label = "이동 범위";

        if (EditMode != AreaEditMode.Movement)
        {
            size = area.DetectionAreaSize;
            handleColor = Color.cyan;
            label = "플레이어 감지 범위";
            if (EditMode == AreaEditMode.Chase)
            {
                using (var areaObject = new SerializedObject(area))
                    margin = Mathf.Max(0f, areaObject.FindProperty("_chaseReleaseMargin").floatValue);
                handleColor = Color.yellow;
                label = "플레이어 추적 범위";
            }
        }

        size = new Vector2(Mathf.Max(0f, size.x), Mathf.Max(0f, size.y));
        using (new Handles.DrawingScope(handleColor, Matrix4x4.identity))
        {
            EditorGUI.BeginChangeCheck();
            if (EditMode == AreaEditMode.Movement)
            {
                _boundsHandle.center = center;
                _boundsHandle.size = new Vector3(size.x, size.y, 0f);
                _boundsHandle.SetColor(handleColor);
                _boundsHandle.DrawHandle();
                center = _boundsHandle.center;
                size = new Vector2(_boundsHandle.size.x, _boundsHandle.size.y);
            }
            else if (EditMode == AreaEditMode.Detection)
            {
                // Resize around the common center without shifting the movement area.
                Handles.DrawWireCube(center, new Vector3(size.x, size.y, 0f));
                size.x = DrawAxisHandles(center, Vector3.right, 0f, size.x * 0.5f) * 2f;
                size.y = DrawAxisHandles(center, Vector3.up, 0f, size.y * 0.5f) * 2f;
            }
            else
            {
                Vector2 chaseSize = size + Vector2.one * (margin * 2f);
                Handles.DrawWireCube(center, new Vector3(chaseSize.x, chaseSize.y, 0f));
                // The runtime uses one margin for all four sides.
                margin = DrawAxisHandles(center, Vector3.right, size.x * 0.5f, margin);
                margin = DrawAxisHandles(center, Vector3.up, size.y * 0.5f, margin);
            }
            bool boundsChanged = EditorGUI.EndChangeCheck();

            float handleSize = HandleUtility.GetHandleSize(center) * 0.08f;
            EditorGUI.BeginChangeCheck();
            center = Handles.Slider2D(center, Vector3.forward, Vector3.right, Vector3.up,
                handleSize, Handles.RectangleHandleCap, Vector2.zero);
            bool centerChanged = EditorGUI.EndChangeCheck();

            if (boundsChanged || centerChanged)
                ApplyBounds(area, origin, center, size, margin, boundsChanged);

            Handles.Label(center + Vector3.up * (size.y * 0.5f + margin + handleSize), label);
        }
    }

    private static float DrawAxisHandles(Vector3 center, Vector3 axis, float baseExtent, float extraExtent)
    {
        for (int sign = -1; sign <= 1; sign += 2)
        {
            Vector3 direction = axis * sign;
            Vector3 position = center + direction * (baseExtent + extraExtent);
            float handleSize = HandleUtility.GetHandleSize(position) * 0.06f;
            EditorGUI.BeginChangeCheck();
            Vector3 dragged = Handles.Slider(position, direction, handleSize, Handles.DotHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
                extraExtent = Mathf.Max(0f, Vector3.Dot(dragged - center, direction) - baseExtent);
        }

        return extraExtent;
    }

    private static void ApplyBounds(EnemyMoveArea area, Vector3 origin, Vector3 center,
        Vector2 size, float margin, bool boundsChanged)
    {
        // Serialized properties preserve Undo and prefab overrides.
        using (var areaObject = new SerializedObject(area))
        {
            areaObject.Update();
            areaObject.FindProperty(nameof(EnemyMoveArea.CenterOffset)).vector2Value =
                new Vector2(center.x - origin.x, center.y - origin.y);

            if (boundsChanged)
            {
                if (EditMode == AreaEditMode.Movement)
                    areaObject.FindProperty(nameof(EnemyMoveArea.AreaSize)).vector2Value = size;
                else if (EditMode == AreaEditMode.Detection)
                {
                    // Start from the effective size, including when detection follows movement.
                    areaObject.FindProperty("_useCustomDetectionArea").boolValue = true;
                    areaObject.FindProperty("_detectionAreaSize").vector2Value = size;
                }
                else
                    areaObject.FindProperty("_chaseReleaseMargin").floatValue = margin;
            }

            Undo.SetCurrentGroupName("Edit Enemy Area");
            areaObject.ApplyModifiedProperties();
        }
    }
}
