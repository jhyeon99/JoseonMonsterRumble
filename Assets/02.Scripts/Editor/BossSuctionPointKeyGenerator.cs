#if UNITY_EDITOR
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 궁극기 차지 애니메이션(Boss_Ultimate_Charge)에 입 위치(SuctionPoint) 키프레임을 자동으로 넣는 에디터 도구.
/// 메뉴: Tools / Boss / 궁극기 차지 입 위치 키 생성
/// 1. PF_Boss 프리팹의 Visual 아래에 SuctionPoint가 없으면 만듦
/// 2. 차지 클립의 스프라이트 키마다, 그 스프라이트(boss_ultimate_charge_000NN)의 입 위치로 SuctionPoint 위치 키를 넣음 (Constant)
/// 입 위치는 스프라이트 이미지에서 추적한 픽셀 좌표 (왼쪽 위 기준). 다시 실행하면 SuctionPoint 위치 키를 덮어씀
/// </summary>
public static class BossSuctionPointKeyGenerator
{
    private const string ClipPath = "Assets/05.Animations/Enemy/Boss/2Phase/Ultimate/Boss_Ultimate_Charge.anim";
    private const string BossPrefabPath = "Assets/03.Prefabs/Enemy/Boss/PF_Boss.prefab";
    private const string VisualName = "Visual";
    private const string PointName = "SuctionPoint";

    /// <summary>boss_ultimate_charge_00000 ~ 00120 각 프레임의 입 위치 (픽셀, 왼쪽 위 기준)</summary>
    private static readonly Vector2[] MouthPixels =
    {
        new Vector2(830, 465), new Vector2(830, 465), new Vector2(830, 466), new Vector2(830, 467), new Vector2(831, 469), new Vector2(828, 459),
        new Vector2(826, 452), new Vector2(825, 430), new Vector2(822, 422), new Vector2(818, 408), new Vector2(817, 405), new Vector2(817, 405),
        new Vector2(817, 405), new Vector2(817, 405), new Vector2(817, 405), new Vector2(817, 405), new Vector2(817, 405), new Vector2(817, 405),
        new Vector2(817, 405), new Vector2(817, 405), new Vector2(817, 405), new Vector2(817, 405), new Vector2(817, 405), new Vector2(817, 405),
        new Vector2(817, 404), new Vector2(817, 404), new Vector2(817, 404), new Vector2(817, 404), new Vector2(817, 404), new Vector2(817, 404),
        new Vector2(817, 403), new Vector2(817, 403), new Vector2(817, 402), new Vector2(817, 402), new Vector2(817, 402), new Vector2(817, 402),
        new Vector2(817, 402), new Vector2(817, 401), new Vector2(817, 401), new Vector2(817, 401), new Vector2(817, 401), new Vector2(817, 401),
        new Vector2(817, 400), new Vector2(817, 400), new Vector2(817, 399), new Vector2(818, 400), new Vector2(818, 401), new Vector2(819, 402),
        new Vector2(819, 403), new Vector2(819, 404), new Vector2(819, 404), new Vector2(819, 403), new Vector2(819, 402), new Vector2(819, 402),
        new Vector2(819, 401), new Vector2(819, 400), new Vector2(819, 399), new Vector2(819, 398), new Vector2(819, 399), new Vector2(819, 400),
        new Vector2(819, 401), new Vector2(820, 401), new Vector2(823, 399), new Vector2(824, 398), new Vector2(824, 397), new Vector2(824, 396),
        new Vector2(824, 394), new Vector2(824, 393), new Vector2(824, 390), new Vector2(824, 389), new Vector2(824, 386), new Vector2(824, 384),
        new Vector2(824, 380), new Vector2(824, 378), new Vector2(824, 374), new Vector2(824, 372), new Vector2(824, 368), new Vector2(824, 366),
        new Vector2(824, 362), new Vector2(824, 360), new Vector2(824, 356), new Vector2(824, 354), new Vector2(824, 350), new Vector2(824, 348),
        new Vector2(824, 344), new Vector2(824, 342), new Vector2(824, 339), new Vector2(824, 338), new Vector2(824, 335), new Vector2(824, 334),
        new Vector2(825, 331), new Vector2(826, 329), new Vector2(828, 319), new Vector2(829, 312), new Vector2(831, 300), new Vector2(832, 292),
        new Vector2(834, 279), new Vector2(835, 271), new Vector2(838, 258), new Vector2(839, 250), new Vector2(842, 238), new Vector2(843, 230),
        new Vector2(846, 219), new Vector2(847, 212), new Vector2(850, 202), new Vector2(851, 196), new Vector2(853, 188), new Vector2(854, 182),
        new Vector2(856, 180), new Vector2(857, 179), new Vector2(859, 178), new Vector2(860, 178), new Vector2(862, 179), new Vector2(863, 179),
        new Vector2(866, 180), new Vector2(866, 180), new Vector2(866, 180), new Vector2(866, 180), new Vector2(866, 180), new Vector2(866, 180),
        new Vector2(866, 180),
    };

    [MenuItem("Tools/Boss/궁극기 차지 입 위치 키 생성")]
    private static void Generate()
    {
        bool pointReady = EnsureSuctionPoint();

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null)
        {
            Debug.LogError($"차지 클립을 찾지 못했습니다: {ClipPath}");
            return;
        }

        EditorCurveBinding spriteBinding = default;
        bool found = false;
        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            if (binding.type == typeof(SpriteRenderer) && binding.propertyName == "m_Sprite")
            {
                spriteBinding = binding;
                found = true;
                break;
            }
        }

        if (!found)
        {
            Debug.LogError("차지 클립에서 스프라이트 키를 찾지 못했습니다.");
            return;
        }

        // 스프라이트 키가 Visual 기준 경로("")라면 SuctionPoint는 그 자식
        string pointPath = string.IsNullOrEmpty(spriteBinding.path) ? PointName : spriteBinding.path + "/" + PointName;

        var curveX = new AnimationCurve();
        var curveY = new AnimationCurve();
        var curveZ = new AnimationCurve();
        int added = 0;

        foreach (ObjectReferenceKeyframe key in AnimationUtility.GetObjectReferenceCurve(clip, spriteBinding))
        {
            var sprite = key.value as Sprite;
            if (sprite == null)
            {
                continue;
            }

            Match match = Regex.Match(sprite.name, @"(\d+)$");
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out int frame) || frame < 0 || frame >= MouthPixels.Length)
            {
                Debug.LogWarning($"입 위치 표에 없는 스프라이트라 건너뜀: {sprite.name}");
                continue;
            }

            // 픽셀(왼쪽 위 기준) → 스프라이트 로컬 좌표 (피벗 기준, Pixels Per Unit)
            Vector2 pixel = MouthPixels[frame];
            Rect rect = sprite.rect;
            float fromLeft = pixel.x - rect.x;
            float fromBottom = (sprite.texture != null ? sprite.texture.height : rect.height) - pixel.y - rect.y;
            float x = (fromLeft - sprite.pivot.x) / sprite.pixelsPerUnit;
            float y = (fromBottom - sprite.pivot.y) / sprite.pixelsPerUnit;

            curveX.AddKey(new Keyframe(key.time, x));
            curveY.AddKey(new Keyframe(key.time, y));
            curveZ.AddKey(new Keyframe(key.time, 0f));
            added++;
        }

        if (added == 0)
        {
            Debug.LogError("넣을 키가 없습니다.");
            return;
        }

        SetConstant(curveX);
        SetConstant(curveY);
        SetConstant(curveZ);

        Undo.RecordObject(clip, "궁극기 차지 입 위치 키 생성");
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(pointPath, typeof(Transform), "m_LocalPosition.x"), curveX);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(pointPath, typeof(Transform), "m_LocalPosition.y"), curveY);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(pointPath, typeof(Transform), "m_LocalPosition.z"), curveZ);
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();

        Debug.Log($"궁극기 차지 입 위치 키 {added}개 생성 완료 (경로: {pointPath})" +
                  (pointReady ? "" : $". {VisualName} 아래에 {PointName}을 직접 만들어 주세요."), clip);
    }

    /// <summary>스프라이트가 바뀌는 순간 그대로 이동하도록 모든 키를 Constant로</summary>
    private static void SetConstant(AnimationCurve curve)
    {
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
        }
    }

    /// <summary>PF_Boss 프리팹의 Visual 아래에 SuctionPoint가 없으면 생성</summary>
    private static bool EnsureSuctionPoint()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(BossPrefabPath);
        if (root == null)
        {
            Debug.LogWarning($"보스 프리팹을 찾지 못했습니다: {BossPrefabPath}");
            return false;
        }

        try
        {
            Transform visual = FindChild(root.transform, VisualName);
            if (visual == null)
            {
                Debug.LogWarning($"보스 프리팹에서 {VisualName}을 찾지 못했습니다.");
                return false;
            }

            if (visual.Find(PointName) != null)
            {
                return true;
            }

            var point = new GameObject(PointName);
            point.transform.SetParent(visual, false);
            PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
            Debug.Log($"{BossPrefabPath}: {VisualName} 아래에 {PointName} 생성");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Transform FindChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform found = FindChild(child, childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
#endif
