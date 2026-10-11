using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CameraBottomBoundaryZone))]
public sealed class CameraBottomBoundaryZoneEditor : Editor
{
    private const float MinimumWidth = 0.01f;
    private const float MinimumHeight = 1f;
    private const float MinimumScaleFactor = 0.000001f;
    private static readonly Color HandleColor = new Color(0.2f, 1f, 0.45f, 1f);

    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "씬의 초록색 범위 좌우·위아래 화살표를 드래그하세요. 반대쪽 끝을 고정한 채 한쪽으로만 늘리거나 줄일 수 있습니다. 실제 카메라 하단 한계는 '화면 하단 경계'로 조절합니다.",
            MessageType.Info);
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            EditorGUILayout.HelpBox("범위를 편집하려면 플레이 모드를 종료하세요.", MessageType.Info);
        DrawDefaultInspector();
    }

    private void OnSceneGUI()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || target == null)
            return;

        var zone = (CameraBottomBoundaryZone)target;
        Transform zoneTransform = zone.transform;
        serializedObject.UpdateIfRequiredOrScript();
        var bottomEdge = serializedObject.FindProperty("_bottomEdge").objectReferenceValue as Transform;
        Vector3 center = zoneTransform.position;
        float halfWidth = Mathf.Abs(zoneTransform.lossyScale.x) * 0.5f;
        float halfHeight = Mathf.Max(MinimumHeight, Mathf.Abs(zoneTransform.lossyScale.y)) * 0.5f;

        using (new Handles.DrawingScope(HandleColor, Matrix4x4.identity))
        {
            if (GetWorldScalePerLocalAxis(zoneTransform, 0) > MinimumScaleFactor)
            {
                if (DrawSideHandle(zoneTransform, bottomEdge,
                    center + Vector3.left * halfWidth, 0, true, "왼쪽 끝"))
                    return;
                if (DrawSideHandle(zoneTransform, bottomEdge,
                    center + Vector3.right * halfWidth, 0, false, "오른쪽 끝"))
                    return;
            }
            if (GetWorldScalePerLocalAxis(zoneTransform, 1) > MinimumScaleFactor)
            {
                if (DrawSideHandle(zoneTransform, bottomEdge,
                    center + Vector3.up * halfHeight, 1, false, "위쪽 끝"))
                    return;
                DrawSideHandle(zoneTransform, bottomEdge,
                    center + Vector3.down * halfHeight, 1, true, "아래쪽 끝");
            }
        }
    }

    private static bool DrawSideHandle(Transform zoneTransform, Transform bottomEdge,
        Vector3 position, int axis, bool minimumSide, string label)
    {
        float size = HandleUtility.GetHandleSize(position) * 0.2f;
        Vector3 direction = (axis == 0 ? Vector3.right : Vector3.up) * (minimumSide ? -1f : 1f);
        Vector3 labelOffset = axis == 0 ? Vector3.up * size : direction * size * 1.4f + Vector3.right * size;
        Handles.Label(position + labelOffset, label);
        EditorGUI.BeginChangeCheck();
        Vector3 dragged = Handles.Slider(position, direction, size, Handles.ArrowHandleCap, 0f);
        if (!EditorGUI.EndChangeCheck())
            return false;

        if (ResizeFromEdge(zoneTransform, bottomEdge, axis, minimumSide, dragged[axis]))
            SceneView.RepaintAll();
        return true;
    }

    internal static bool ResizeFromSide(Transform zoneTransform, Transform bottomEdge,
        bool leftSide, float newWorldX)
    {
        return ResizeFromEdge(zoneTransform, bottomEdge, 0, leftSide, newWorldX);
    }

    internal static bool ResizeVerticallyFromSide(Transform zoneTransform, Transform bottomEdge,
        bool bottomSide, float newWorldY)
    {
        return ResizeFromEdge(zoneTransform, bottomEdge, 1, bottomSide, newWorldY);
    }

    private static bool ResizeFromEdge(Transform zoneTransform, Transform bottomEdge,
        int axis, bool minimumSide, float newWorldCoordinate)
    {
        if (zoneTransform == null || float.IsNaN(newWorldCoordinate) || float.IsInfinity(newWorldCoordinate))
            return false;

        float scaleFactor = GetWorldScalePerLocalAxis(zoneTransform, axis);
        if (scaleFactor <= MinimumScaleFactor)
            return false;

        Vector3 center = zoneTransform.position;
        float minimumSize = axis == 0 ? MinimumWidth : MinimumHeight;
        float visibleSize = axis == 0 ? Mathf.Abs(zoneTransform.lossyScale.x) :
            Mathf.Max(MinimumHeight, Mathf.Abs(zoneTransform.lossyScale.y));
        float lower = center[axis] - visibleSize * 0.5f;
        float upper = center[axis] + visibleSize * 0.5f;
        float oldEdge = minimumSide ? lower : upper;
        if (minimumSide)
            lower = Mathf.Min(newWorldCoordinate, upper - minimumSize);
        else
            upper = Mathf.Max(newWorldCoordinate, lower + minimumSize);
        if (Mathf.Approximately(oldEdge, minimumSide ? lower : upper))
            return false;

        // A child boundary marker must keep its world position while its parent is resized.
        bool preserveBottomEdge = bottomEdge != null && bottomEdge != zoneTransform &&
            bottomEdge.IsChildOf(zoneTransform);
        Vector3 bottomPosition = preserveBottomEdge ? bottomEdge.position : Vector3.zero;
        if (preserveBottomEdge)
            Undo.RecordObjects(new Object[] { zoneTransform, bottomEdge }, "카메라 제한 범위 편집");
        else
            Undo.RecordObject(zoneTransform, "카메라 제한 범위 편집");

        // Match the world-aligned gizmo, including scaled, rotated, or mirrored parents.
        Vector3 localScale = zoneTransform.localScale;
        float sign = localScale[axis] < 0f ? -1f : 1f;
        localScale[axis] = sign * (upper - lower) / scaleFactor;
        zoneTransform.localScale = localScale;
        center[axis] = lower + (upper - lower) * 0.5f;
        zoneTransform.position = center;
        if (preserveBottomEdge)
        {
            bottomEdge.position = bottomPosition;
            PrefabUtility.RecordPrefabInstancePropertyModifications(bottomEdge);
            EditorUtility.SetDirty(bottomEdge);
        }

        PrefabUtility.RecordPrefabInstancePropertyModifications(zoneTransform);
        EditorUtility.SetDirty(zoneTransform);
        return true;
    }

    private static float GetWorldScalePerLocalAxis(Transform zoneTransform, int axis)
    {
        if (zoneTransform.parent == null)
            return 1f;
        float localAxisScale = Mathf.Abs(zoneTransform.localScale[axis]);
        if (localAxisScale > MinimumScaleFactor)
            return Mathf.Abs(zoneTransform.lossyScale[axis]) / localAxisScale;

        Vector3 unitAxisScale = zoneTransform.localScale;
        unitAxisScale[axis] = 1f;
        Matrix4x4 worldMatrix = zoneTransform.parent.localToWorldMatrix *
            Matrix4x4.TRS(Vector3.zero, zoneTransform.localRotation, unitAxisScale);
        return Mathf.Abs(worldMatrix.lossyScale[axis]);
    }
}
