using UnityEngine;
using UnityEngine.Rendering.Universal;

[ExecuteAlways, DisallowMultipleComponent]
public sealed class SpriteLightContactShadow : MonoBehaviour
{
    [SerializeField] private Collider2D _body;
    [SerializeField] private SpriteRenderer _artwork;
    [SerializeField] private Light2D _shadowLight;
    [SerializeField] private LayerMask _surfaceLayers;
    [SerializeField] private Transform _bossLandingPoint;
    [SerializeField, Min(0.01f)] private float _fadeInDuration = 0.18f;
    [SerializeField, Range(0.1f, 1f)] private float _widthRatio = 0.9f;
    [SerializeField, Min(0.01f)] private float _shadowThickness = 0.1f;
    [SerializeField, Range(0f, 1f)] private float _opacity = 0.6f;

    private const int CookieSize = 64;
    private const float ContactTolerance = 0.005f;
    private const float MinimumSurfaceNormalY = 0.7f;
    private const float SurfaceProbeDistance = 0.02f;
    private readonly ContactPoint2D[] _contacts = new ContactPoint2D[8];
    private readonly RaycastHit2D[] _surfaceHits = new RaycastHit2D[4];
    private ContactFilter2D _surfaceFilter;
    private Player _player;
    private Boss _boss;
    private Texture2D _cookieTexture;
    private Sprite _cookieSprite;
    private float _appearance;

    private void OnEnable()
    {
        if (_body == null || _artwork == null || _shadowLight == null || _surfaceLayers.value == 0)
        {
            Debug.LogWarning("SpriteLightContactShadow: 몸체, 그림, Light 2D, 바닥 레이어 연결이 필요합니다.", this);
            enabled = false;
            return;
        }
        _player = GetComponent<Player>();
        _boss = GetComponent<Boss>();
        _surfaceFilter = new ContactFilter2D { useTriggers = false };
        _surfaceFilter.SetLayerMask(_surfaceLayers);
        CreateCookieSprite();
        _shadowLight.lightType = Light2D.LightType.Sprite;
        _shadowLight.lightCookieSprite = _cookieSprite;
        _shadowLight.blendStyleIndex = 0;
        _shadowLight.overlapOperation = Light2D.OverlapOperation.AlphaBlend;
        _shadowLight.lightOrder = 100;
        _shadowLight.intensity = 1f;
        _shadowLight.shadowsEnabled = false;
        _shadowLight.volumeIntensityEnabled = false;
        _shadowLight.enabled = false;
    }

    private void CreateCookieSprite()
    {
        _cookieTexture = new Texture2D(CookieSize, CookieSize, TextureFormat.RGBA32, false)
        {
            name = "SpriteLightShadowCookie",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[CookieSize * CookieSize];
        for (int y = 0; y < CookieSize; y++)
        {
            for (int x = 0; x < CookieSize; x++)
            {
                Vector2 uv = new Vector2((x + 0.5f) / CookieSize, (y + 0.5f) / CookieSize) * 2f - Vector2.one;
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - uv.magnitude));
                pixels[y * CookieSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }
        _cookieTexture.SetPixels32(pixels);
        _cookieTexture.Apply(false, false);
        _cookieSprite = Sprite.Create(_cookieTexture, new Rect(0, 0, CookieSize, CookieSize),
            new Vector2(0.5f, 0.5f), CookieSize, 0, SpriteMeshType.FullRect);
        _cookieSprite.hideFlags = HideFlags.HideAndDontSave;
    }

    private void LateUpdate()
    {
        if (_shadowLight == null || _cookieSprite == null) return;
        if (_body == null || !_body.enabled || _artwork == null || !_artwork.enabled ||
            (Application.IsPlaying(gameObject) && (_body.isTrigger || (_player != null && !_player.IsGrounded))) ||
            !TryGetSurface(out Collider2D surface, out Vector2 point))
        {
            HideShadow();
            return;
        }
        _appearance = Application.IsPlaying(gameObject)
            ? Mathf.MoveTowards(_appearance, 1f, Time.deltaTime / Mathf.Max(0.01f, _fadeInDuration)) : 1f;
        float width = Mathf.Min(_body.bounds.size.x * _widthRatio, surface.bounds.size.x);
        float centerX = Mathf.Clamp(_body.bounds.center.x, surface.bounds.min.x + width * 0.5f,
            surface.bounds.max.x - width * 0.5f);
        var position = new Vector3(centerX, point.y - _shadowThickness * 0.5f, transform.position.z);
        var scale = new Vector3(width, _shadowThickness, 1f);
        var color = new Color(0f, 0f, 0f, _opacity * Mathf.SmoothStep(0f, 1f, _appearance));
        if (_shadowLight.transform.position != position) _shadowLight.transform.position = position;
        if (_shadowLight.transform.localScale != scale) _shadowLight.transform.localScale = scale;
        if (_shadowLight.color != color) _shadowLight.color = color;
        if (!_shadowLight.enabled) _shadowLight.enabled = true;
    }

    private bool TryGetSurface(out Collider2D surface, out Vector2 point)
    {
        surface = null;
        point = default;
        if (Application.IsPlaying(gameObject) && _player != null)
        {
            int count = _body.GetContacts(_surfaceFilter, _contacts);
            for (int i = 0; i < count; i++)
            {
                ContactPoint2D contact = _contacts[i];
                if (contact.normal.y <= MinimumSurfaceNormalY || contact.normalImpulse <= 0f || contact.separation > ContactTolerance) continue;
                surface = contact.collider == _body ? contact.otherCollider : contact.collider;
                if (surface == null || surface == _body) continue;
                point = contact.point;
                return true;
            }
            return false;
        }
        if (Application.IsPlaying(gameObject) && _boss != null)
        {
            Vector2 position = _boss.Position;
            bool isOnGround = Mathf.Abs(position.y - _boss.GroundY) <= ContactTolerance;
            bool isAtLandingPoint = _bossLandingPoint != null &&
                Vector2.Distance(position, _bossLandingPoint.position) <= ContactTolerance;
            if (!isOnGround && !isAtLandingPoint) return false;
        }
        Vector2 origin = _body.bounds.center;
        float distance = Mathf.Max(0f, origin.y - transform.position.y) + SurfaceProbeDistance;
        int hitCount = Physics2D.Raycast(origin, Vector2.down, _surfaceFilter, _surfaceHits, distance);
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = _surfaceHits[i];
            if (hit.collider == null || hit.collider == _body || hit.normal.y <= MinimumSurfaceNormalY || hit.fraction <= 0f) continue;
            surface = hit.collider;
            point = hit.point;
            return true;
        }
        return false;
    }

    private void HideShadow()
    {
        _appearance = 0f;
        if (_shadowLight != null && _shadowLight.enabled) _shadowLight.enabled = false;
    }

    private void OnDisable()
    {
        HideShadow();
        if (_shadowLight != null) _shadowLight.lightCookieSprite = null;
        if (Application.IsPlaying(gameObject))
        {
            Destroy(_cookieSprite);
            Destroy(_cookieTexture);
        }
        else
        {
            DestroyImmediate(_cookieSprite);
            DestroyImmediate(_cookieTexture);
        }
        _cookieSprite = null;
        _cookieTexture = null;
    }
}
