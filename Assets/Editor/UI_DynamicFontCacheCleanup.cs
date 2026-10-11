using TMPro;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class UI_DynamicFontCacheCleanup
{
    private const string FontAssetFilter = "t:TMP_FontAsset";
    private const string FontFolder = "Assets/09.Fonts";
    private const string ClearDynamicDataProperty = "m_ClearDynamicDataOnBuild";
    private static readonly string[] FontSearchFolders = new[] { FontFolder };

    static UI_DynamicFontCacheCleanup()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }

        // Wait until Play Mode teardown is complete before saving font assets.
        EditorApplication.delayCall -= ClearDynamicFontCaches;
        EditorApplication.delayCall += ClearDynamicFontCaches;
    }

    private static void ClearDynamicFontCaches()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        string[] fontGuids = AssetDatabase.FindAssets(FontAssetFilter, FontSearchFolders);
        foreach (string fontGuid in fontGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(fontGuid);
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (fontAsset == null || fontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            {
                continue;
            }

            // Honour the font's existing cache-clear policy; preserve prebuilt fonts.
            using var fontSettings = new SerializedObject(fontAsset);
            SerializedProperty clearDynamicData = fontSettings.FindProperty(ClearDynamicDataProperty);
            if (clearDynamicData == null)
            {
                Debug.LogWarning("동적 폰트 정리: 생성 데이터 정리 설정을 찾을 수 없습니다: " + path, fontAsset);
                continue;
            }

            if (!clearDynamicData.boolValue || fontAsset.characterTable.Count == 0)
            {
                continue;
            }

            if (fontAsset.sourceFontFile == null)
            {
                Debug.LogWarning("동적 폰트 정리: 원본 폰트가 없어 생성 데이터를 유지합니다: " + path, fontAsset);
                continue;
            }

            fontAsset.ClearFontAssetData(true);
            EditorUtility.SetDirty(fontAsset);
            // Save this font only; never save unrelated scenes or assets.
            AssetDatabase.SaveAssetIfDirty(fontAsset);
        }
    }
}
