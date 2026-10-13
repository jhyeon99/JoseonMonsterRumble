using UnityEngine;

/// <summary>
/// 보스 침(BossSpitProjectile)이 날아가는 동안 흘리는 작은 방울.
/// 침 그림을 아주 작게 그려 실제 중력으로 떨어지고, 수명 동안 작아지며 투명해진 뒤 꺼짐.
/// 침과 따로 움직여서 침이 터지거나 풀로 돌아가도 마저 떨어짐 (침이 런타임에 생성 / 재사용)
/// </summary>
public class BossSpitDroplet : MonoBehaviour
{
    private SpriteRenderer _renderer;
    private Vector2 _velocity;
    private float _gravity;
    private float _lifetime;
    private float _elapsed;
    private Vector3 _baseScale;
    private Color _baseColor;
    private bool _spriteFacesLeft;

    public bool IsPlaying => gameObject.activeSelf;

    public void Initialize(Material material)
    {
        _renderer = gameObject.AddComponent<SpriteRenderer>();
        _renderer.sharedMaterial = material;
        gameObject.SetActive(false);
    }

    public void Play(SpriteRenderer source, Vector3 position, Vector2 velocity, float gravity, Vector3 scale, float lifetime, bool spriteFacesLeft)
    {
        _renderer.sprite = source.sprite;
        _renderer.flipX = source.flipX;
        _renderer.flipY = source.flipY;
        _renderer.sortingLayerID = source.sortingLayerID;
        _renderer.sortingOrder = source.sortingOrder - 1;

        _baseColor = source.color;
        _renderer.color = _baseColor;

        _velocity = velocity;
        _gravity = gravity;
        _lifetime = Mathf.Max(0.05f, lifetime);
        _elapsed = 0f;
        _baseScale = scale;
        _spriteFacesLeft = spriteFacesLeft;

        transform.position = position;
        transform.localScale = scale;
        UpdateRotation();
        gameObject.SetActive(true);
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        _elapsed += deltaTime;
        if (_elapsed >= _lifetime)
        {
            gameObject.SetActive(false);
            return;
        }

        Vector2 acceleration = Vector2.down * _gravity;
        transform.position += (Vector3)((_velocity * deltaTime) + (acceleration * (0.5f * deltaTime * deltaTime)));
        _velocity += acceleration * deltaTime;
        UpdateRotation();

        // 후반부에 작아지며 투명해짐
        float t = _elapsed / _lifetime;
        float fade = t < 0.5f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (t - 0.5f) / 0.5f);
        transform.localScale = _baseScale * Mathf.Lerp(0.6f, 1f, fade);

        Color color = _baseColor;
        color.a *= fade;
        _renderer.color = color;
    }

    private void UpdateRotation()
    {
        if (_velocity.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float angle = Mathf.Atan2(_velocity.y, _velocity.x) * Mathf.Rad2Deg;
        if (_spriteFacesLeft)
        {
            angle += 180f;
        }

        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}
