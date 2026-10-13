using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

[CustomEditor(typeof(EnemyMoveArea))]
public sealed class EnemyMoveAreaEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.LabelField("씬에서 범위 편집", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "범위 편집 버튼을 누른 뒤 씬의 변 핸들을 드래그해 크기를 조절하세요. 가운데 핸들은 이동·감지·추적 범위의 공통 중심을 옮깁니다.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            DrawEditButton(EnemyMoveAreaSceneTool.AreaEditMode.Movement, "몬스터 이동 범위");
            DrawEditButton(EnemyMoveAreaSceneTool.AreaEditMode.Detection, "플레이어 감지 범위");
            DrawEditButton(EnemyMoveAreaSceneTool.AreaEditMode.Chase, "플레이어 추적 범위");
        }

        EditorGUILayout.Space();
        DrawDefaultInspector();
        DrawPlatformGuide((EnemyMoveArea)target);

        if (ToolManager.activeToolType == typeof(EnemyMoveAreaSceneTool))
        {
            if (EnemyMoveAreaSceneTool.EditMode == EnemyMoveAreaSceneTool.AreaEditMode.Detection)
                EditorGUILayout.HelpBox("하늘색 범위를 조절하면 이동 범위와 별도로 감지 크기가 저장됩니다.", MessageType.Info);
            else if (EnemyMoveAreaSceneTool.EditMode == EnemyMoveAreaSceneTool.AreaEditMode.Chase)
                EditorGUILayout.HelpBox("노란 범위를 벗어나면 추적을 멈춥니다. 변을 드래그하면 사방의 추적 여유 폭이 함께 바뀝니다.", MessageType.Info);
        }
    }

    private static void DrawPlatformGuide(EnemyMoveArea area)
    {
        EnemyGroundMove groundMove = area.GetComponent<EnemyGroundMove>();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("플랫폼에 맞추는 기준", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(groundMove != null
            ? "녹색 이동 범위의 좌우 끝을 같은 높이의 연속된 발판 안쪽에 맞추세요. 지상 몬스터는 X축으로만 걷고, 실제 절벽 앞에서는 별도로 멈춥니다. 감지 범위를 높게 잡아도 다른 발판으로 점프하지는 않습니다."
            : "녹색 이동 범위 안에 비행 경로 전체가 들어가도록 X와 Y를 함께 조절하세요.", MessageType.Info);
        EditorGUILayout.HelpBox(
            "하늘색은 플레이어를 발견하는 범위, 노란색은 발견 후 추적을 유지하는 범위입니다. 두 범위는 몬스터를 따라 움직입니다. 가운데 핸들을 옮기면 세 범위의 중심 오프셋이 함께 바뀝니다.",
            MessageType.Info);

        Vector2 start = Application.isPlaying ? area.StartPosition : (Vector2)area.transform.position;
        float halfWidth = Mathf.Max(0f, area.AreaSize.x) * 0.5f;
        float left = start.x + area.CenterOffset.x - halfWidth;
        float right = start.x + area.CenterOffset.x + halfWidth;
        EditorGUILayout.LabelField("이동 X 한계 (몬스터 기준점)", $"{left:0.##} ~ {right:0.##}");
        if (area.AreaSize.x <= 0f || (groundMove == null && area.AreaSize.y <= 0f))
            EditorGUILayout.HelpBox("이동 범위 크기가 0 이하이면 순찰할 공간이 없습니다.", MessageType.Warning);
        if (start.x < left || start.x > right)
            EditorGUILayout.HelpBox("시작 위치가 이동 범위 밖에 있습니다. 귀환 지점도 포함되도록 녹색 범위의 중심이나 좌우 끝을 조절하세요.", MessageType.Warning);
        if (groundMove != null)
        {
            Collider2D body = area.GetComponent<Collider2D>();
            if (body != null && !body.isTrigger)
                EditorGUILayout.LabelField("몸체 충돌체 폭", $"{body.bounds.size.x:0.##}");
        }

        if (!Application.isPlaying || groundMove == null) return;
        Enemy enemy = area.GetComponent<Enemy>();
        if (enemy == null || enemy.Data == null) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("현재 이동 상태", EditorStyles.boldLabel);
        if (!enemy.isActiveAndEnabled)
        {
            EditorGUILayout.HelpBox("현재 몬스터가 비활성 상태라 순찰하지 않습니다. 카메라 근처 활성화 조건이나 튜토리얼 대기 상태를 확인하세요.", MessageType.Info);
            return;
        }
        EnemyState state = enemy.StateMachine?.CurrentState;
        EditorGUILayout.LabelField("행동", GetStateLabel(state));
        bool grounded = groundMove.IsGrounded();
        bool canLeft = grounded && groundMove.CanMoveInDirection(-1f);
        bool canRight = grounded && groundMove.CanMoveInDirection(1f);
        EditorGUILayout.LabelField("바닥 지지", grounded ? "감지됨" : "감지 안 됨");
        EditorGUILayout.LabelField("왼쪽 / 오른쪽 지형", $"{(canLeft ? "이동 가능" : "막힘")} / {(canRight ? "이동 가능" : "막힘")}");
        if (!grounded)
            EditorGUILayout.HelpBox("바닥 지지가 감지되지 않아 걷기를 멈춥니다. 발판에 실제로 닿는 몸체 충돌체와 Ground / OneWayPlatform 레이어를 확인하세요.", MessageType.Warning);
        else if (!canLeft && !canRight)
            EditorGUILayout.HelpBox("양쪽 이동이 막혀 있습니다. 녹색 범위가 너무 좁거나, 몸체 앞쪽과 제동 거리까지 받쳐줄 발판이 부족한지 확인하세요.", MessageType.Warning);
        if (state is ChaseState && enemy.Target != null && enemy.Combat is EnemyMeleeCombat melee &&
            !melee.IsTargetInAttackHeight(enemy.Target))
            EditorGUILayout.HelpBox("플레이어를 추적하고 있지만 공격 가능한 높이가 아닙니다. 다른 층의 플레이어를 감지하도록 범위를 넓히면 지상 몬스터는 이동 끝에서 기다릴 수 있습니다.", MessageType.Info);
    }

    private static string GetStateLabel(EnemyState state)
    {
        if (state == null) return "초기화 대기";
        if (state is IdleState) return "순찰";
        if (state is ChaseState) return "플레이어 추적";
        if (state is AlertState) return "플레이어 탐색";
        if (state is ReturnToStartState) return "시작 위치로 복귀";
        return state.GetType().Name;
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

        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(12f, window.position.height - 88f, 350f, 66f), EditorStyles.helpBox);
        GUILayout.Label("씬에서 편집할 범위", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        EditMode = (AreaEditMode)GUILayout.Toolbar((int)EditMode,
            new[] { "몬스터 이동", "플레이어 감지", "플레이어 추적" });
        if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
        GUILayout.EndArea();
        Handles.EndGUI();

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
