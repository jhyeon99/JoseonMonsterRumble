using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Unity에서 길 안내용 불꽃 셰이더를 컴파일해 확인한다.</summary>
public static class BlueGuideFlameValidation
{
    [InitializeOnLoadMethod]
    private static void ScheduleValidation()
    {
        EditorApplication.delayCall += Validate;
    }

    [MenuItem("Tools/Team06/Validate Blue Guide Flame")]
    public static void Validate()
    {
        const string shaderPath = "Assets/10.Materials/S_BlueGuideFlame.shader";
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        if (shader == null) return;

        var variants = new ShaderVariantCollection();
        variants.Add(new ShaderVariantCollection.ShaderVariant(shader, PassType.Normal));
        variants.WarmUp();
        Object.DestroyImmediate(variants);
        var report = new System.Text.StringBuilder();
        bool hasErrors = ShaderUtil.ShaderHasError(shader);
        report.AppendLine(hasErrors ? "FAIL: BlueGuideFlame shader" : "PASS: BlueGuideFlame shader");
        report.AppendLine("Shader revision: " + File.GetLastWriteTimeUtc(shaderPath).ToString("O"));
        foreach (var message in ShaderUtil.GetShaderMessages(shader))
            report.AppendLine(message.severity + ": " + message.message + " (line " + message.line + ")");
        Directory.CreateDirectory("Temp");
        File.WriteAllText("Temp/BlueGuideFlameValidation.txt", report.ToString());
        if (hasErrors) Debug.LogError(report.ToString(), shader);
        else RenderPreview();
    }

    private static void RenderPreview()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/03.Prefabs/Effect/PF_BlueGuideFlame.prefab");
        if (prefab == null) return;
        var preview = new PreviewRenderUtility();
        RenderTexture copy = null;
        Texture2D image = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            GameObject flame = Object.Instantiate(prefab);
            preview.AddSingleGO(flame);
            preview.camera.orthographic = true;
            preview.camera.orthographicSize = 0.75f;
            preview.camera.transform.position = new Vector3(0f, 0f, -5f);
            preview.camera.transform.rotation = Quaternion.identity;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(0.06f, 0.07f, 0.09f);
            preview.camera.nearClipPlane = 0.1f;
            preview.camera.farClipPlane = 10f;
            preview.BeginPreview(new Rect(0, 0, 512, 512), GUIStyle.none);
            preview.Render(true);
            Texture rendered = preview.EndPreview();
            copy = RenderTexture.GetTemporary(512, 512, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(rendered, copy);
            RenderTexture.active = copy;
            image = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
            image.Apply();
            File.WriteAllBytes("Temp/BlueGuideFlamePreview.png", image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            if (image != null) Object.DestroyImmediate(image);
            if (copy != null) RenderTexture.ReleaseTemporary(copy);
            preview.Cleanup();
        }
    }
}

public sealed class BlueGuideFlameImportValidation : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
        string[] moved, string[] movedFrom)
    {
        foreach (string path in imported)
        {
            if (path != "Assets/10.Materials/S_BlueGuideFlame.shader" &&
                path != "Assets/10.Materials/M_BlueGuideFlame.mat") continue;
            EditorApplication.delayCall += BlueGuideFlameValidation.Validate;
            break;
        }
    }
}
