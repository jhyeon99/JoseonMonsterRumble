using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 바닥에서 솟아오르는 가시 (찍기 패턴의 마지막 공격). 패턴이 Begin으로 위치 / 시간을 넘겨 줌.
/// 바닥 아래에서 위로 솟구침 → 잠깐 유지 → 다시 내려간 뒤 사라짐. 솟아 있는 동안 플레이어에게 한 번 데미지.
/// 콜라이더(Trigger) 필요. 바닥 아래에 있을 때 안 보이게 하려면 Sorting Order를 바닥 스프라이트보다 낮게.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BossFloorSpike : MonoBehaviour
{
    [Header("데미지")]
    [Tooltip("공격 이름 (사망 원인 표시용)")]
    [SerializeField]
    private string _attackName = "바닥 가시";

    [SerializeField, Min(0f)] private float _damage = 15f;
    [SerializeField, Min(0f)] private float _knockbackForce = 6f;

    [Tooltip("맞으면 걸리는 독 지속 시간 (초). 0이면 독 없음")]
    [SerializeField, Min(0f)] private float _poisonDuration = 10f;

    [SerializeField]
    private AttackDefense _defense = new AttackDefense { Parryable = false, GuardGaugeCost = 30f };

    [Tooltip("솟아오른 정도(0 ~ 1)가 이 값 이상일 때만 데미지 (바닥 아래에서 스치기만 해도 맞지 않도록)")]
    [SerializeField, Range(0f, 1f)]
    private float _damageRiseThreshold = 0.3f;

    [Header("모양")]
    [Tooltip("스프라이트를 위로 세우는 회전 각도. 원본이 오른쪽을 보면 90, 왼쪽을 보면 -90, 이미 위를 보면 0")]
    [SerializeField]
    private float _uprightRotation = 90f;

    private readonly List<Collider2D> _results = new List<Collider2D>();
    private Collider2D _collider;
    private ContactFilter2D _filter;
    private Boss _boss;
    private Vector3 _upPosition;
    private Vector3 _downPosition;
    private float _riseTime;
    private float _holdTime;
    private float _sinkTime;
    private float _elapsed;
    private bool _isRunning;
    private bool _hasHit;

    private bool _visible = true;
    private Renderer[] _renderers;

    private void SetVisible(bool visible)
    {
        _visible = visible;
        if (_renderers == null)
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        foreach (Renderer renderer in _renderers)
        {
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }
    }

    // 땅 앞면 아래 잘라내기: 잘린 길이를 이 단계로 나눠 미리 만든 스프라이트를 씀 (매 프레임 생성하지 않도록)
    private const int GroundClipSteps = 32;
    private static readonly Dictionary<(Sprite, int), Sprite[]> ClippedSpriteCache = new Dictionary<(Sprite, int), Sprite[]>();
    private SpriteRenderer _clipRenderer;
    private Sprite _clipSourceSprite;
    private bool _useGroundClip;
    private float _groundClipY;

    /// <summary>
    /// groundEdgeY(월드) 아래로 내려간 부분을 잘라 안 보이게 함. 땅 앞면(흙벽) 뒤에서 윗면을 뚫고 솟는 것처럼 보임.
    /// 마스크(스텐실)를 쓰지 않고 스프라이트 자체를 잘라서 그림
    /// </summary>
    public void UseGroundClip(float groundEdgeY)
    {
        _clipRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (_clipRenderer == null || _clipRenderer.sprite == null)
        {
            return;
        }

        _clipSourceSprite = _clipRenderer.sprite;
        _groundClipY = groundEdgeY;
        _useGroundClip = true;
        ApplyGroundClip();
    }

    private void ApplyGroundClip()
    {
        if (!_useGroundClip || _clipRenderer == null)
        {
            return;
        }

        // 스프라이트의 로컬 축 중 월드 위쪽을 향하는 축 (세운 가시는 보통 로컬 X)
        Transform rendererTransform = _clipRenderer.transform;
        Vector3 localUp = rendererTransform.InverseTransformDirection(Vector3.up);
        bool alongX = Mathf.Abs(localUp.x) >= Mathf.Abs(localUp.y);
        int sign = (alongX ? localUp.x : localUp.y) >= 0f ? 1 : -1;

        Sprite source = _clipSourceSprite;
        float length = alongX ? source.rect.width : source.rect.height;
        float pivot = alongX ? source.pivot.x : source.pivot.y;
        float axisScale = Mathf.Abs(alongX ? rendererTransform.lossyScale.x : rendererTransform.lossyScale.y);
        if (length <= 0f || axisScale <= 0f)
        {
            return;
        }

        // 잘리는 선의 위치 (축 방향 픽셀). sign > 0이면 그보다 큰 쪽, sign < 0이면 작은 쪽이 땅 위
        float cutPixel = pivot + (sign * (_groundClipY - rendererTransform.position.y) * source.pixelsPerUnit / axisScale);
        float visiblePixels = sign > 0 ? length - cutPixel : cutPixel;
        int step = Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp01(visiblePixels / length) * GroundClipSteps), 0, GroundClipSteps);

        if (step >= GroundClipSteps)
        {
            _clipRenderer.sprite = source;
        }
        else if (step <= 0)
        {
            _clipRenderer.sprite = null;
        }
        else
        {
            _clipRenderer.sprite = GetClippedSprite(source, alongX, sign, step);
        }
    }

    /// <summary>땅 위로 보이는 쪽(위쪽 끝)만 남긴 스프라이트. 기준점은 원본과 같은 자리라 위치가 그대로</summary>
    private static Sprite GetClippedSprite(Sprite source, bool alongX, int sign, int step)
    {
        int key = (alongX ? 2 : 0) + (sign > 0 ? 1 : 0);
        if (!ClippedSpriteCache.TryGetValue((source, key), out Sprite[] sprites))
        {
            sprites = new Sprite[GroundClipSteps];
            ClippedSpriteCache[(source, key)] = sprites;
        }

        if (sprites[step] != null)
        {
            return sprites[step];
        }

        Rect rect = source.packed && source.packingMode == SpritePackingMode.Rectangle ? source.textureRect : source.rect;
        float length = alongX ? rect.width : rect.height;
        float keep = Mathf.Max(1f, Mathf.Round(length * step / GroundClipSteps));
        float removed = length - keep;
        Vector2 pivotPixels = source.pivot;

        if (alongX)
        {
            if (sign > 0)
            {
                rect.x += removed;
                pivotPixels.x -= removed;
            }

            rect.width = keep;
        }
        else
        {
            if (sign > 0)
            {
                rect.y += removed;
                pivotPixels.y -= removed;
            }

            rect.height = keep;
        }

        var pivot = new Vector2(pivotPixels.x / rect.width, pivotPixels.y / rect.height);
        var clipped = Sprite.Create(source.texture, rect, pivot, source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        clipped.name = $"{source.name}_Clip{step}";
        sprites[step] = clipped;
        return clipped;
    }

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        _collider.isTrigger = true;

        _filter = new ContactFilter2D();
        _filter.SetLayerMask(Constants.Layers.PlayerMask);
        _filter.useTriggers = true;
    }

    /// <summary>upPosition = 다 솟았을 때 위치. 그보다 riseHeight만큼 아래에서 시작. delay초 뒤에 솟음 (그동안 숨김)</summary>
    public void Begin(Boss boss, Vector2 upPosition, float riseHeight, float riseTime, float holdTime, float sinkTime, float delay = 0f)
    {
        _boss = boss;
        if (_boss != null)
        {
            // 페이즈 전환 등에서 ClearMinions로 같이 정리
            _boss.RegisterMinion(gameObject);
        }

        _upPosition = new Vector3(upPosition.x, upPosition.y, transform.position.z);
        _downPosition = _upPosition + (Vector3.down * Mathf.Max(0f, riseHeight));
        _riseTime = Mathf.Max(0.01f, riseTime);
        _holdTime = Mathf.Max(0f, holdTime);
        _sinkTime = Mathf.Max(0.01f, sinkTime);
        _elapsed = -Mathf.Max(0f, delay);
        _hasHit = false;
        _isRunning = true;
        SetVisible(_elapsed >= 0f);

        transform.SetPositionAndRotation(_downPosition, Quaternion.Euler(0f, 0f, _uprightRotation));
        ApplyGroundClip();
    }

    private void Update()
    {
        if (!_isRunning)
        {
            return;
        }

        _elapsed += Time.deltaTime;

        // 순서대로 솟는 파도: 차례가 올 때까지 숨긴 채 대기
        if (_elapsed < 0f)
        {
            return;
        }

        if (!_visible)
        {
            SetVisible(true);
        }

        float amount;
        if (_elapsed < _riseTime)
        {
            // 솟을 때는 빠르게 튀어나왔다가 끝에서 감속
            float t = _elapsed / _riseTime;
            amount = 1f - ((1f - t) * (1f - t));
        }
        else if (_elapsed < _riseTime + _holdTime)
        {
            amount = 1f;
        }
        else if (_elapsed < _riseTime + _holdTime + _sinkTime)
        {
            // 내려갈 때는 천천히 시작해서 점점 빠르게
            float t = (_elapsed - _riseTime - _holdTime) / _sinkTime;
            amount = 1f - (t * t);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        transform.position = Vector3.Lerp(_downPosition, _upPosition, amount);
        ApplyGroundClip();

        if (!_hasHit && amount >= _damageRiseThreshold)
        {
            CheckHit();
        }
    }

    private void CheckHit()
    {
        _results.Clear();
        int count = Physics2D.OverlapCollider(_collider, _filter, _results);

        for (int i = 0; i < count; i++)
        {
            Collider2D other = _results[i];
            if (other.GetComponentInParent<Player>() == null)
            {
                continue;
            }

            IDamageable damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null)
            {
                continue;
            }

            // 무적(회피 등) 중이면 이번 프레임은 넘기고, 무적이 끝난 뒤 아직 솟아 있으면 맞음
            if (damageable is PlayerHealth playerHealth && playerHealth.IsInvincible)
            {
                continue;
            }

            var hitData = new HitData(
                _damage,
                _knockbackForce,
                other.ClosestPoint(transform.position),
                Vector2.up,
                _defense,
                null,
                attackName: _attackName
            );
            hitData.Attacker = _boss != null ? _boss.gameObject : gameObject;
            hitData.PoisonDuration = _poisonDuration;

            damageable.TakeDamage(hitData);
            _hasHit = true;
            return;
        }
    }

    private void OnDestroy()
    {
        if (_boss != null)
        {
            _boss.UnregisterMinion(gameObject);
        }
    }
}
