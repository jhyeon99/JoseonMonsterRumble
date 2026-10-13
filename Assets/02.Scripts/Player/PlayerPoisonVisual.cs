using UnityEngine;

/// <summary>
/// 독에 걸린 동안 플레이어 스프라이트를 초록빛으로 은은하게 깜빡임.
/// SpriteRenderer.color의 RGB만 바꾸고 알파(무적 반투명)는 건드리지 않음.
/// 피격 반짝임(HitFlash) 등 다른 곳에서 색을 바꾼 프레임은 그 색을 우선하고, 끝나면 다시 초록빛을 얹음.
/// PlayerHealth가 없으면 붙여도 동작하지 않음 (PlayerHealth가 자동으로 추가함)
/// </summary>
[DisallowMultipleComponent]
public class PlayerPoisonVisual : MonoBehaviour
{
    [Tooltip("독 색 (스프라이트에 곱해지는 쪽으로 섞임)")]
    [SerializeField] private Color _poisonTint = new Color(0.55f, 1f, 0.4f, 1f);

    [Tooltip("박동이 가장 약할 때 초록빛 세기 (0~1)")]
    [SerializeField, Range(0f, 1f)] private float _minStrength = 0.3f;

    [Tooltip("박동이 가장 강할 때 초록빛 세기 (0~1)")]
    [SerializeField, Range(0f, 1f)] private float _maxStrength = 0.7f;

    [Tooltip("깜빡이는 주기 (초)")]
    [SerializeField, Min(0.1f)] private float _pulsePeriod = 0.9f;

    [Tooltip("독이 걸리고 풀릴 때 서서히 나타나고 사라지는 시간 (초)")]
    [SerializeField, Min(0.01f)] private float _fadeTime = 0.25f;

    private PlayerHealth _health;
    private SpriteRenderer _renderer;
    private float _weight;
    private float _phase;

    // 독 색을 얹기 전의 원래 색과, 마지막으로 얹어 놓은 색 (다른 곳에서 색을 바꿨는지 알아내기 위함)
    private Color _baseColor = Color.white;
    private Color _appliedColor;
    private bool _hasApplied;

    private void Awake()
    {
        _health = GetComponent<PlayerHealth>();
        _renderer = GetComponentInChildren<SpriteRenderer>();
        if (_renderer != null)
        {
            _baseColor = _renderer.color;
        }
    }

    private void OnDisable()
    {
        Restore();
        _weight = 0f;
    }

    private void LateUpdate()
    {
        if (_renderer == null || _health == null)
        {
            return;
        }

        bool poisoned = _health.IsPoisoned;
        _weight = Mathf.MoveTowards(_weight, poisoned ? 1f : 0f, Time.unscaledDeltaTime / _fadeTime);

        if (_weight <= 0f)
        {
            Restore();
            return;
        }

        Color current = _renderer.color;

        // 마지막으로 얹은 색과 다르면 다른 곳(피격 반짝임 등)에서 바꾼 것 → 그 색을 새 기준으로
        if (!_hasApplied || !SameRgb(current, _appliedColor))
        {
            _baseColor = current;
        }

        _phase = Mathf.Repeat(_phase + (Time.unscaledDeltaTime / _pulsePeriod), 1f);
        float pulse = 0.5f - (0.5f * Mathf.Cos(_phase * Mathf.PI * 2f));
        float strength = _weight * Mathf.Lerp(_minStrength, _maxStrength, pulse);

        // 피격 반짝임처럼 원래 색과 크게 다른 순간은 그 색을 그대로 보여 줌
        bool isFlashing = !SameRgb(_baseColor, Color.white) && _baseColor.g < 0.5f;
        Color tinted = isFlashing ? _baseColor : Color.Lerp(_baseColor, _baseColor * _poisonTint, strength);
        tinted.a = current.a;

        _renderer.color = tinted;
        _appliedColor = tinted;
        _hasApplied = true;
    }

    /// <summary>독 색을 걷어내고 원래 색으로 (알파는 현재 값 유지)</summary>
    private void Restore()
    {
        if (!_hasApplied || _renderer == null)
        {
            _hasApplied = false;
            return;
        }

        Color current = _renderer.color;
        if (SameRgb(current, _appliedColor))
        {
            Color restored = _baseColor;
            restored.a = current.a;
            _renderer.color = restored;
        }

        _hasApplied = false;
    }

    private static bool SameRgb(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.002f && Mathf.Abs(a.g - b.g) < 0.002f && Mathf.Abs(a.b - b.b) < 0.002f;
    }
}
