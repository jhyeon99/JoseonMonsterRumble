using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(200)]
public sealed class BackgroundSeamBlend : MonoBehaviour
{
    [SerializeField, Min(0.1f), Tooltip("경계 양쪽을 섞는 전체 폭입니다. 월드 단위입니다.")]
    private float _blendWidth = 3f;
    [SerializeField] private Material _blendMaterial;
    private readonly List<Seam> _seams = new List<Seam>();
    private Mesh _mesh;

    private sealed class Seam
    {
        public SpriteRenderer Left;
        public SpriteRenderer Right;
        public MeshRenderer Renderer;
        public readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Test_Ep1") return;
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.GetComponentInChildren<BackgroundSeamBlend>(true) != null) return;
        GameObject prefab = Resources.Load<GameObject>("BackgroundSeamBlend");
        if (prefab != null)
        {
            GameObject instance = Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(instance, scene);
        }
    }

    private void Start()
    {
        if (_blendMaterial == null) return;
        var backgrounds = new List<SpriteRenderer>();
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.sprite == null) continue;
                string textureName = renderer.sprite.texture.name;
                if (textureName == "배경1" || textureName == "배경2") backgrounds.Add(renderer);
            }
        }
        backgrounds.Sort((a, b) => a.bounds.center.x.CompareTo(b.bounds.center.x));
        _mesh = new Mesh { name = "BackgroundSeamQuad" };
        _mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
        _mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        _mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        _mesh.RecalculateBounds();
        for (int i = 0; i < backgrounds.Count; i++)
        {
            for (int j = i + 1; j < backgrounds.Count; j++)
            {
                SpriteRenderer left = backgrounds[i];
                SpriteRenderer right = backgrounds[j];
                if (left.sortingLayerID != right.sortingLayerID || left.sortingOrder != right.sortingOrder) continue;
                float tolerance = Mathf.Min(left.bounds.size.x, right.bounds.size.x) * 0.05f;
                if (Mathf.Abs(left.bounds.max.x - right.bounds.min.x) > tolerance) continue;
                if (Mathf.Abs(left.bounds.center.y - right.bounds.center.y) > 0.5f) continue;
                var patch = new GameObject("BackgroundSeam", typeof(MeshFilter), typeof(MeshRenderer));
                patch.transform.SetParent(transform, false);
                patch.GetComponent<MeshFilter>().sharedMesh = _mesh;
                MeshRenderer renderer = patch.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = _blendMaterial;
                renderer.sortingLayerID = left.sortingLayerID;
                renderer.sortingOrder = left.sortingOrder + 1;
                _seams.Add(new Seam { Left = left, Right = right, Renderer = renderer });
            }
        }
    }

    private void LateUpdate()
    {
        foreach (Seam seam in _seams)
        {
            SpriteRenderer left = seam.Left;
            SpriteRenderer right = seam.Right;
            bool visible = left != null && right != null && left.enabled && right.enabled &&
                left.gameObject.activeInHierarchy && right.gameObject.activeInHierarchy;
            seam.Renderer.enabled = visible;
            if (!visible) continue;
            Bounds a = left.bounds;
            Bounds b = right.bounds;
            float width = Mathf.Min(_blendWidth, Mathf.Min(a.size.x, b.size.x) * 0.25f);
            if (Mathf.Abs(a.max.x - b.min.x) > width * 0.5f)
            {
                seam.Renderer.enabled = false;
                continue;
            }
            float bottom = Mathf.Max(a.min.y, b.min.y);
            float top = Mathf.Min(a.max.y, b.max.y);
            Transform patch = seam.Renderer.transform;
            patch.position = new Vector3((a.max.x + b.min.x) * 0.5f, (bottom + top) * 0.5f, a.center.z);
            patch.localScale = new Vector3(width, Mathf.Max(0f, top - bottom), 1f);
            SetSprite(seam.Properties, "_Left", left);
            SetSprite(seam.Properties, "_Right", right);
            seam.Renderer.SetPropertyBlock(seam.Properties);
        }
    }

    private static void SetSprite(MaterialPropertyBlock properties, string prefix, SpriteRenderer renderer)
    {
        Sprite sprite = renderer.sprite;
        Rect rect = sprite.textureRect;
        Texture texture = sprite.texture;
        Bounds bounds = renderer.bounds;
        properties.SetTexture(prefix + "Tex", texture);
        properties.SetVector(prefix + "Bounds", new Vector4(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y));
        // Sample texel centers, including sprites cut from a sheet.
        properties.SetVector(prefix + "UV", new Vector4((rect.x + 0.5f) / texture.width,
            (rect.y + 0.5f) / texture.height, (rect.width - 1f) / texture.width, (rect.height - 1f) / texture.height));
        bool flipX = renderer.flipX ^ (renderer.transform.lossyScale.x < 0f);
        bool flipY = renderer.flipY ^ (renderer.transform.lossyScale.y < 0f);
        properties.SetVector(prefix + "Flip", new Vector4(flipX ? 1f : 0f, flipY ? 1f : 0f, 0f, 0f));
        properties.SetColor(prefix + "Color", renderer.color);
    }

    private void OnDisable()
    {
        foreach (Seam seam in _seams)
            if (seam.Renderer != null) seam.Renderer.enabled = false;
    }

    private void OnDestroy()
    {
        if (_mesh != null) Destroy(_mesh);
    }
}
