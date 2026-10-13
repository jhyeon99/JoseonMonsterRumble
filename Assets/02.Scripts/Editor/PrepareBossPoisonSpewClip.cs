using UnityEditor;
using UnityEngine;

/// <summary>선택한 패턴의 실제 계층에 맞춘 독분사 클립만 생성한다. 보스 Controller는 변경하지 않는다.</summary>
public static class PrepareBossPoisonSpewClip
{
    private const string SourceClipPath = "Assets/05.Animations/VFX/Anim_BossPoisonSpewPreview.anim";

    [MenuItem("Tools/Poison Spew/Prepare Clip For Selected Pattern")]
    private static void CreateIntegrationClip()
    {
        BossPoisonSpewPattern pattern = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<BossPoisonSpewPattern>() : null;
        if (pattern == null || PrefabUtility.IsPartOfPrefabAsset(pattern))
        {
            Debug.LogWarning("독분사 접목: 씬에 배치한 PF_BossPoisonSpewPattern을 선택하세요.");
            return;
        }
        var serialized = new SerializedObject(pattern);
        var mouth = serialized.FindProperty("_mouth").objectReferenceValue as Transform;
        var sprite = serialized.FindProperty("_bossRenderer").objectReferenceValue as SpriteRenderer;
        Animator animator = mouth != null ? mouth.GetComponentInParent<Animator>() : null;
        AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>(SourceClipPath);
        if (mouth == null || sprite == null || animator == null || source == null
            || !sprite.transform.IsChildOf(animator.transform))
        {
            Debug.LogWarning("독분사 접목: 패턴과 입을 보스 Animator 아래에 배치하고 Boss Renderer를 연결하세요.", pattern);
            return;
        }
        // 검수된 로컬 입 키를 바꾸지 않도록, 중간 패턴 Transform은 항등 변환이어야 한다.
        Transform anchor = mouth.parent;
        while (anchor != null && anchor != sprite.transform)
        {
            if (anchor.localPosition.sqrMagnitude > 0.000001f
                || Quaternion.Angle(anchor.localRotation, Quaternion.identity) > 0.001f
                || (anchor.localScale - Vector3.one).sqrMagnitude > 0.000001f)
            {
                Debug.LogWarning("독분사 접목: 입과 보스 Sprite 사이 오브젝트의 로컬 위치·회전은 0, 크기는 1이어야 합니다.", pattern);
                return;
            }
            anchor = anchor.parent;
        }
        if (anchor != sprite.transform)
        {
            Debug.LogWarning("독분사 접목: 패턴을 Boss Renderer 오브젝트의 자식으로 배치하세요.", pattern);
            return;
        }
        AnimationClip clip = Object.Instantiate(source);
        clip.name = "Anim_BossPoisonSpew_Integrated";
        string mouthPath = AnimationUtility.CalculateTransformPath(mouth, animator.transform);
        string spritePath = AnimationUtility.CalculateTransformPath(sprite.transform, animator.transform);
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
        {
            if (binding.path != "Mouth" || binding.type != typeof(Transform)) continue;
            AnimationCurve curve = AnimationUtility.GetEditorCurve(source, binding);
            AnimationUtility.SetEditorCurve(clip, binding, null);
            EditorCurveBinding destination = binding;
            destination.path = mouthPath;
            AnimationUtility.SetEditorCurve(clip, destination, curve);
        }
        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(source))
        {
            if (binding.type != typeof(SpriteRenderer)) continue;
            ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(source, binding);
            AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
            EditorCurveBinding destination = binding;
            destination.path = spritePath;
            AnimationUtility.SetObjectReferenceCurve(clip, destination, keys);
        }
        string destinationPath = AssetDatabase.GenerateUniqueAssetPath("Assets/05.Animations/VFX/Anim_BossPoisonSpew_Integrated.anim");
        AssetDatabase.CreateAsset(clip, destinationPath);
        Undo.RecordObject(pattern, "독분사 클립 연결");
        serialized.Update();
        serialized.FindProperty("_animationClip").objectReferenceValue = clip;
        float totalFrames = Mathf.Max(1f, clip.length * clip.frameRate);
        serialized.FindProperty("_hitStartRatio").floatValue = Mathf.Clamp01(10f / totalFrames);
        serialized.FindProperty("_hitEndRatio").floatValue = Mathf.Clamp01(38f / totalFrames);
        serialized.FindProperty("_followThroughLeadRatio").floatValue = 0.55f / Mathf.Max(0.01f, clip.length);
        serialized.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(pattern);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(clip);
        Debug.Log("독분사 접목 클립 생성 완료. 이 클립을 보스 Animator의 독분사 상태에 연결하고 전이를 설정하세요. Controller는 변경하지 않았습니다.", pattern);
    }
}
