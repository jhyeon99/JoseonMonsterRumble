using UnityEditor;
using UnityEngine;

public static class BuildEP1BlockoutGroundPrefab
{
    private const string SpritePath =
        "Assets/04.Sprites/Environment/PrototypePlatforms/DirtTrail/EP1_Blockout_Ground_v1.png";
    private const string PrefabPath =
        "Assets/04.Sprites/Environment/PrototypePlatforms/DirtTrail/EP1_Blockout_Ground_v1.prefab";

    [MenuItem("Tools/EP1/Create Blockout Ground Prefab")]
    public static void CreatePrefab()
    {
        Sprite groundSprite = null;
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(SpritePath))
        {
            if (asset is Sprite sprite && sprite.name == "EP1_Blockout_Ground_v1")
            {
                groundSprite = sprite;
                break;
            }
        }

        if (groundSprite == null)
        {
            Debug.LogError("EP1 ground sprite could not be loaded: " + SpritePath);
            return;
        }

        GameObject root = new GameObject("EP1_Blockout_Ground_v1");
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0) root.layer = groundLayer;

        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = groundSprite;
        renderer.sortingLayerName = "EnvironmentBack";

        EP1BlockoutGroundCollider colliderSetup = root.AddComponent<EP1BlockoutGroundCollider>();
        colliderSetup.ApplyShape();

        bool succeeded;
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out succeeded);
        Object.DestroyImmediate(root);

        if (!succeeded)
        {
            Debug.LogError("EP1 ground prefab was not saved: " + PrefabPath);
            return;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("EP1 ground prefab ready: " + PrefabPath);
    }
}
