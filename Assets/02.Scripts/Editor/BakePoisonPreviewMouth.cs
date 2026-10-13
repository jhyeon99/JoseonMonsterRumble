using UnityEditor;
using UnityEngine;

/// <summary>Bakes only the isolated preview clip. Never modifies the boss clip or controller.</summary>
[InitializeOnLoad]
public static class BakePoisonPreviewMouth
{
    private const string PreviewClipPath = "Assets/05.Animations/VFX/Anim_BossPoisonSpewPreview.anim";
    private const string SpriteFolder = "Assets/04.Sprites/Enemy/Boss/Attack/PoisonSpew/";
    private const float FrameRate = 16f;
    // Frame-specific mouth ink measurements, followed by a lip-side offset.
    private static readonly Vector3[] MouthPixels =
    {
        new Vector3(854,547,20), new Vector3(855,545,20), new Vector3(855,543,20),
        new Vector3(855,541,20), new Vector3(856,538,20), new Vector3(856,535,20),
        new Vector3(856,534,20), new Vector3(856,535,20), new Vector3(856,537,20),
        new Vector3(856,539,20), new Vector3(856,542,20), new Vector3(856,545,20),
        new Vector3(855,546,20), new Vector3(855,545,20), new Vector3(854,543,20),
        new Vector3(854,539,20), new Vector3(854,530,23), new Vector3(853,506,27),
        new Vector3(850,472,30), new Vector3(841,431,40), new Vector3(827,388,50),
        new Vector3(811,355,54), new Vector3(797,331,58), new Vector3(793,325,59),
        new Vector3(790,322,59), new Vector3(788,319,60), new Vector3(786,317,60),
        new Vector3(786,317,60)
    };

    static BakePoisonPreviewMouth()
    {
        EditorApplication.delayCall += InitializePreviewMouthKeys;
    }

    private static void InitializePreviewMouthKeys()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(PreviewClipPath);
        if (clip == null)
            return;
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (binding.path == "Mouth")
                return;
        }
        BakeMouthKeys();
    }

    [MenuItem("Tools/Poison Preview/Rebuild Mouth Keys")]
    public static void BakeMouthKeys()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(PreviewClipPath);
        if (clip == null)
        {
            Debug.LogWarning("독분사 테스트: 임시 애니메이션 클립을 찾지 못했습니다.");
            return;
        }
        var xCurve = new AnimationCurve();
        var yCurve = new AnimationCurve();
        var angleCurve = new AnimationCurve();
        for (int i = 0; i < MouthPixels.Length; i++)
        {
            int frame = i + 10;
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + $"Boss_PoisonSpew_{frame:D5}.png");
            if (sprite == null)
            {
                Debug.LogWarning($"독분사 테스트: 입 키 생성에 필요한 {frame}번 Sprite가 없습니다.");
                return;
            }
            Vector3 pixel = MouthPixels[i];
            float radians = pixel.z * Mathf.Deg2Rad;
            float x = (pixel.x / 1280f * sprite.rect.width - sprite.pivot.x) / sprite.pixelsPerUnit
                - Mathf.Cos(radians) * 0.04f;
            float y = ((1f - pixel.y / 720f) * sprite.rect.height - sprite.pivot.y) / sprite.pixelsPerUnit
                - Mathf.Sin(radians) * 0.04f;
            float time = frame / FrameRate;
            xCurve.AddKey(time, x);
            yCurve.AddKey(time, y);
            angleCurve.AddKey(time, pixel.z);
        }
        SetLinearKeys(xCurve);
        SetLinearKeys(yCurve);
        SetLinearKeys(angleCurve);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Mouth", typeof(Transform), "m_LocalPosition.x"), xCurve);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Mouth", typeof(Transform), "m_LocalPosition.y"), yCurve);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Mouth", typeof(Transform), "localEulerAnglesRaw.z"), angleCurve);
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssetIfDirty(clip);
    }

    private static void SetLinearKeys(AnimationCurve curve)
    {
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }
    }
}
