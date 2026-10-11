using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class UI_EnemyHealthBars : MonoBehaviour
{
    private const string MissingReferencesWarning = "UI_EnemyHealthBars: Canvas, HP artwork or fill materials are missing.";
    private const string MissingManagerWarning = "UI_EnemyHealthBars: EnemyManager is missing.";
    private const string MissingHealthWarning = "UI_EnemyHealthBars: enemy health or data is missing.";
    private const string BarName = "EnemyHealthBar";
    private const string BackgroundName = "Background";
    private const string MainMaskName = "MainMask";
    private const string GhostMaskName = "GhostMask";
    private const string FillName = "Fill";
    private const string FrameName = "Frame";
    private const string ShieldMaskName = "ShieldMask";
    private const string HealMaskName = "HealMask";
    private const float MinimumDuration = 0.01f;
    private static readonly int HighlightColorId = Shader.PropertyToID("_HighlightColor");
    private static readonly Vector2 LeftCenter = new Vector2(0f, 0.5f);
    private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

    [Header("공용 HP 이미지")]
    [SerializeField] private Texture2D _backgroundTexture;
    [SerializeField] private Texture2D _fillTexture;
    [SerializeField] private Texture2D _frameTexture;
    [SerializeField] private Material _fillMaterial;
    [SerializeField] private Material _damageGhostMaterial;
    [Tooltip("원본 이미지의 공통 영역입니다. X/Y는 시작점, Z/W는 너비/높이입니다.")]
    [SerializeField] private Vector4 _artUvRegion = new Vector4(0.1f, 0.37f, 0.8f, 0.26f);
    [Header("모든 몬스터 공통 배치")]
    [SerializeField] private Camera _worldCamera;
    [SerializeField] private Vector2 _barSize = new Vector2(234f, 38f);
    [SerializeField] private Vector2 _screenOffset = new Vector2(0f, -28f);
    [Header("HP 잔상")]
    [SerializeField, Min(0f)] private float _damageDelay = 0.25f;
    [SerializeField, Min(MinimumDuration)] private float _damageCatchupDuration = 0.35f;
    [SerializeField, Min(MinimumDuration)] private float _healCatchupDuration = 0.2f;
    [Header("도깨비불 효과")]
    [SerializeField] private Color _shieldOverlayColor = new Color(1f, 1f, 1f, 0.38f);
    [SerializeField] private Color _healOverlayColor = new Color(0.12f, 0.78f, 0.32f, 0.85f);
    [SerializeField, Min(MinimumDuration)] private float _healTintDuration = 0.35f;
    [SerializeField, Range(0f, 1f)] private float _healTintMaxWidthRatio = 0.06f;

    private enum TrailPhase
    {
        Stable,
        DamageDelay,
        DamageCatchup,
        HealCatchup
    }

    private sealed class UI_HealthBar
    {
        public Enemy Enemy;
        public EnemyHealth Health;
        public Collider2D Body;
        public RectTransform Root;
        public RectTransform MainMask;
        public RectTransform GhostMask;
        public RectTransform ShieldMask;
        public RectTransform HealMask;
        public RectTransform HealArtwork;
        public RawImage HealImage;
        public float HealStartRatio;
        public float HealTintRemaining;
        public float TargetRatio;
        public float MainRatio;
        public float GhostRatio;
        public float StartRatio;
        public float Elapsed;
        public TrailPhase Phase;
    }

    private RectTransform _container;
    private Canvas _canvas;
    private Material _overlayMaterial;
    private readonly List<UI_HealthBar> _bars = new List<UI_HealthBar>();
    private readonly Dictionary<Enemy, UI_HealthBar> _barsByEnemy = new Dictionary<Enemy, UI_HealthBar>();

    private void Awake()
    {
        _container = (RectTransform)transform;
        _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null || _backgroundTexture == null || _fillTexture == null || _frameTexture == null ||
            _fillMaterial == null || _damageGhostMaterial == null)
        {
            Debug.LogWarning(MissingReferencesWarning, this);
            enabled = false;
            return;
        }
        // One owned material for all bars; image colours distinguish the two overlays.
        _overlayMaterial = new Material(_damageGhostMaterial);
        _overlayMaterial.SetColor(HighlightColorId, Color.white);
    }

    private void OnDestroy()
    {
        if (_overlayMaterial != null)
        {
            Destroy(_overlayMaterial);
        }
    }

    private void Start()
    {
        if (EnemyManager.Instance == null)
        {
            Debug.LogWarning(MissingManagerWarning, this);
        }
    }

    private void OnDisable()
    {
        foreach (UI_HealthBar bar in _bars)
        {
            if (bar.Root != null)
            {
                Destroy(bar.Root.gameObject);
            }
        }
        _bars.Clear();
        _barsByEnemy.Clear();
    }

    private void LateUpdate()
    {
        EnemyManager manager = EnemyManager.Instance;
        Camera worldCamera = _worldCamera != null ? _worldCamera : Camera.main;
        if (manager == null || worldCamera == null)
        {
            SetBarsVisible(false);
            return;
        }

        List<Enemy> enemies = manager.ActiveEnemies;
        foreach (Enemy enemy in enemies)
        {
            if (enemy != null && enemy.isActiveAndEnabled && !enemy.IsDefeated && !_barsByEnemy.ContainsKey(enemy))
            {
                CreateHealthBar(enemy);
            }
        }

        Canvas rootCanvas = _canvas.rootCanvas;
        Camera uiCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        float deltaTime = PauseController.IsPaused ? 0f : Time.unscaledDeltaTime;
        for (int index = _bars.Count - 1; index >= 0; index--)
        {
            UI_HealthBar bar = _bars[index];
            if (bar.Enemy == null || !bar.Enemy.isActiveAndEnabled || bar.Health == null ||
                !bar.Health.IsAlive || bar.Enemy.IsDefeated)
            {
                _barsByEnemy.Remove(bar.Enemy);
                Destroy(bar.Root.gameObject);
                _bars.RemoveAt(index);
                continue;
            }

            UpdateHealth(bar, deltaTime);
            UpdateBuffOverlays(bar, deltaTime);
            UpdatePosition(bar, worldCamera, uiCamera);
        }
    }

    private void SetBarsVisible(bool isVisible)
    {
        foreach (UI_HealthBar bar in _bars)
        {
            bar.Root.gameObject.SetActive(isVisible);
        }
    }

    private void CreateHealthBar(Enemy enemy)
    {
        if (enemy.Health == null || enemy.Data == null)
        {
            Debug.LogWarning(MissingHealthWarning, enemy);
            return;
        }
        if (!enemy.Health.IsAlive)
        {
            return;
        }

        var bar = new UI_HealthBar();
        bar.Enemy = enemy;
        bar.Health = enemy.Health;
        enemy.TryGetComponent(out bar.Body);
        bar.Root = CreateRect(BarName, _container, _barSize, Center);
        CreateArtwork(BackgroundName, bar.Root, _backgroundTexture, Color.white, Center);
        bar.GhostMask = CreateMask(GhostMaskName, bar.Root, _damageGhostMaterial);
        bar.MainMask = CreateMask(MainMaskName, bar.Root, _fillMaterial);
        bar.HealMask = CreateMask(HealMaskName, bar.Root, _overlayMaterial);
        RawImage healArtwork = bar.HealMask.GetComponentInChildren<RawImage>();
        healArtwork.color = _healOverlayColor;
        bar.HealImage = healArtwork;
        bar.HealArtwork = healArtwork.rectTransform;
        bar.ShieldMask = CreateMask(ShieldMaskName, bar.Root, _overlayMaterial);
        bar.ShieldMask.GetComponentInChildren<RawImage>().color = _shieldOverlayColor;
        CreateArtwork(FrameName, bar.Root, _frameTexture, Color.white, Center);
        bar.TargetRatio = CalculateHealthRatio(bar);
        bar.MainRatio = bar.TargetRatio;
        bar.GhostRatio = bar.TargetRatio;
        ApplyMaskWidths(bar);
        UpdateBuffOverlays(bar, 0f);
        _bars.Add(bar);
        _barsByEnemy.Add(enemy, bar);
    }

    private RectTransform CreateRect(string objectName, RectTransform parent, Vector2 size, Vector2 pivot)
    {
        var uiObject = new GameObject(objectName, typeof(RectTransform));
        uiObject.layer = gameObject.layer;
        var rect = (RectTransform)uiObject.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = pivot;
        rect.anchorMax = pivot;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        return rect;
    }

    private RawImage CreateArtwork(string objectName, RectTransform parent, Texture2D texture, Color color, Vector2 pivot)
    {
        RectTransform rect = CreateRect(objectName, parent, _barSize, pivot);
        RawImage image = rect.gameObject.AddComponent<RawImage>();
        image.texture = texture;
        image.uvRect = new Rect(_artUvRegion.x, _artUvRegion.y, _artUvRegion.z, _artUvRegion.w);
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private RectTransform CreateMask(string objectName, RectTransform parent, Material material)
    {
        RectTransform mask = CreateRect(objectName, parent, _barSize, LeftCenter);
        mask.gameObject.AddComponent<RectMask2D>();
        // Clip a full-width image rather than shrinking and stretching the brush artwork.
        RawImage image = CreateArtwork(FillName, mask, _fillTexture, Color.white, LeftCenter);
        image.material = material;
        return mask;
    }

    private static float CalculateHealthRatio(UI_HealthBar bar)
    {
        float maxHp = bar.Enemy.Data.MaxHp;
        return maxHp > 0f ? Mathf.Clamp01(bar.Health.CurrentHp / maxHp) : 0f;
    }

    private void UpdateHealth(UI_HealthBar bar, float deltaTime)
    {
        float targetRatio = CalculateHealthRatio(bar);
        if (!Mathf.Approximately(targetRatio, bar.TargetRatio))
        {
            bool isDamage = targetRatio < bar.TargetRatio;
            if (isDamage)
            {
                bar.HealTintRemaining = 0f;
                bar.HealStartRatio = targetRatio;
            }
            else
            {
                float healStart = bar.HealTintRemaining > 0f ? bar.HealStartRatio : bar.TargetRatio;
                bar.HealStartRatio = Mathf.Max(healStart, targetRatio - _healTintMaxWidthRatio);
                bar.HealTintRemaining = _healTintDuration;
            }
            bar.TargetRatio = targetRatio;
            bar.Elapsed = 0f;
            if (isDamage)
            {
                bar.MainRatio = targetRatio;
                bar.GhostRatio = Mathf.Max(bar.GhostRatio, targetRatio);
                bar.StartRatio = bar.GhostRatio;
                bar.Phase = TrailPhase.DamageDelay;
            }
            else
            {
                bar.GhostRatio = targetRatio;
                bar.StartRatio = bar.MainRatio;
                bar.Phase = TrailPhase.HealCatchup;
            }
        }

        if (bar.Phase == TrailPhase.Stable)
        {
            return;
        }
        bar.Elapsed += deltaTime;
        switch (bar.Phase)
        {
            case TrailPhase.DamageDelay:
                if (bar.Elapsed >= _damageDelay)
                {
                    bar.Elapsed = 0f;
                    bar.StartRatio = bar.GhostRatio;
                    bar.Phase = TrailPhase.DamageCatchup;
                }
                break;
            case TrailPhase.DamageCatchup:
                float damageProgress = Mathf.Clamp01(bar.Elapsed / Mathf.Max(MinimumDuration, _damageCatchupDuration));
                bar.GhostRatio = Mathf.Lerp(bar.StartRatio, bar.TargetRatio, Mathf.SmoothStep(0f, 1f, damageProgress));
                if (damageProgress >= 1f)
                {
                    ResetTrail(bar);
                }
                break;
            case TrailPhase.HealCatchup:
                float healProgress = Mathf.Clamp01(bar.Elapsed / Mathf.Max(MinimumDuration, _healCatchupDuration));
                bar.MainRatio = Mathf.Lerp(bar.StartRatio, bar.TargetRatio, Mathf.SmoothStep(0f, 1f, healProgress));
                if (healProgress >= 1f)
                {
                    ResetTrail(bar);
                }
                break;
        }
        ApplyMaskWidths(bar);
    }

    private static void ResetTrail(UI_HealthBar bar)
    {
        bar.Phase = TrailPhase.Stable;
        bar.Elapsed = 0f;
        bar.StartRatio = 0f;
        bar.MainRatio = bar.TargetRatio;
        bar.GhostRatio = bar.TargetRatio;
    }

    private void ApplyMaskWidths(UI_HealthBar bar)
    {
        bar.MainMask.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _barSize.x * bar.MainRatio);
        bar.GhostMask.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _barSize.x * bar.GhostRatio);
    }

    private void UpdateBuffOverlays(UI_HealthBar bar, float deltaTime)
    {
        float maxHp = bar.Enemy.Data.MaxHp;
        float shieldRatio = maxHp > 0f ? Mathf.Clamp01(bar.Health.ShieldHp / maxHp) : 0f;
        bar.ShieldMask.gameObject.SetActive(shieldRatio > 0f);
        bar.ShieldMask.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _barSize.x * shieldRatio);

        // The full brush stays aligned with HP; only the newly filling interval is revealed.
        bar.HealTintRemaining = Mathf.Max(0f, bar.HealTintRemaining - deltaTime);
        float healWidth = bar.HealTintRemaining > 0f
            ? Mathf.Max(0f, bar.TargetRatio - bar.HealStartRatio) * _barSize.x : 0f;
        bar.HealMask.gameObject.SetActive(healWidth > 0f);
        float healStart = bar.HealStartRatio * _barSize.x;
        bar.HealMask.anchoredPosition = new Vector2(healStart, 0f);
        bar.HealMask.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, healWidth);
        bar.HealArtwork.anchoredPosition = new Vector2(-healStart, 0f);
        Color healColor = _healOverlayColor;
        healColor.a *= Mathf.Clamp01(bar.HealTintRemaining / Mathf.Max(MinimumDuration, _healTintDuration));
        bar.HealImage.color = healColor;
    }

    private void UpdatePosition(UI_HealthBar bar, Camera worldCamera, Camera uiCamera)
    {
        Vector3 position = bar.Enemy.transform.position;
        if (bar.Body != null && bar.Body.enabled)
        {
            Bounds bounds = bar.Body.bounds;
            position = new Vector3(bounds.center.x, bounds.min.y, position.z);
        }
        Vector3 screenPosition = worldCamera.WorldToScreenPoint(position);
        Rect cameraRect = worldCamera.pixelRect;
        bool isVisible = screenPosition.z > 0f && cameraRect.Contains(screenPosition);
        bar.Root.gameObject.SetActive(isVisible);
        if (isVisible && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _container, screenPosition, uiCamera, out Vector2 localPosition))
        {
            bar.Root.localPosition = localPosition + _screenOffset;
        }
    }
}
