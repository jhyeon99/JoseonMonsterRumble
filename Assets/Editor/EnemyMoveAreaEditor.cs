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
            "씬 편집 버튼을 누른 뒤 사각형의 변 핸들을 드래그하면 크기를, 가운데 핸들을 드래그하면 중심 위치를 조절할 수 있습니다.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            bool isEditing = ToolManager.activeToolType == typeof(EnemyMoveAreaSceneTool);
            if (GUILayout.Button(isEditing ? "이동 범위 편집 마치기" : "씬에서 이동 범위 편집"))
            {
                if (isEditing)
                    ToolManager.RestorePreviousTool();
                else
                    ToolManager.SetActiveTool<EnemyMoveAreaSceneTool>();

                SceneView.RepaintAll();
            }
        }
    }
}

[EditorTool("이동 범위 편집", typeof(EnemyMoveArea))]
public sealed class EnemyMoveAreaSceneTool : EditorTool
{
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
                    tooltip = "씬에서 적 이동 범위 편집"
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

        // The movement code uses world X/Y units, independent of rotation and scale.
        Vector3 origin = area.transform.position;
        _boundsHandle.center = origin + new Vector3(area.CenterOffset.x, area.CenterOffset.y, 0f);
        _boundsHandle.size = new Vector3(Mathf.Max(0f, area.AreaSize.x), Mathf.Max(0f, area.AreaSize.y), 0f);

        Color handleColor = new Color(area.AreaColor.r, area.AreaColor.g, area.AreaColor.b, 1f);
        _boundsHandle.SetColor(handleColor);

        using (new Handles.DrawingScope(handleColor, Matrix4x4.identity))
        {
            EditorGUI.BeginChangeCheck();
            _boundsHandle.DrawHandle();

            Vector3 center = _boundsHandle.center;
            float handleSize = HandleUtility.GetHandleSize(center) * 0.08f;
            center = Handles.Slider2D(center, Vector3.forward, Vector3.right, Vector3.up,
                handleSize, Handles.RectangleHandleCap, Vector2.zero);

            if (EditorGUI.EndChangeCheck())
            {
                // Serialized properties preserve Undo and prefab overrides.
                using (var areaObject = new SerializedObject(area))
                {
                    areaObject.Update();
                    areaObject.FindProperty(nameof(EnemyMoveArea.CenterOffset)).vector2Value =
                        new Vector2(center.x - origin.x, center.y - origin.y);
                    areaObject.FindProperty(nameof(EnemyMoveArea.AreaSize)).vector2Value =
                        new Vector2(Mathf.Max(0f, _boundsHandle.size.x), Mathf.Max(0f, _boundsHandle.size.y));
                    Undo.SetCurrentGroupName("Edit Enemy Move Area");
                    areaObject.ApplyModifiedProperties();
                }
            }

            Handles.Label(center + Vector3.up * (_boundsHandle.size.y * 0.5f + handleSize), "이동 범위");
        }
    }
}
