using UnityEngine;

/// <summary>
/// 패링하면 그로기가 되는 공격 직전에 몬스터를 반짝이게 하는 예고 연출.
/// EnemyHitFeedback처럼 원본 스프라이트 위에 흰 실루엣 스프라이트(Flash Renderer)를 겹쳐 그려 밝게 빛나게 함.
/// - 일반 적: 적 오브젝트에 붙이면 지상 근거리 공격 준비 중 판정 시작 Lead Time초 전에 자동 재생
/// - 보스: 보스 오브젝트(또는 자식)에 붙이면 패링 그로기 판정(찍기 등) 시작 Lead Time초 전에 자동 재생
/// Flash Renderer는 피격 플래시와 겹치지 않도록 별도 자식 오브젝트로 두기 (같은 흰색 머티리얼 사용 가능).
/// </summary>
[DisallowMultipleComponent]
public class ParryWarningFlash : MonoBehaviour
{
    [Header("렌더러")]
    [Tooltip("원본 스프라이트. 비우면 자식에서 첫 SpriteRenderer를 찾음 (Flash Renderer 제외)")]
    [SerializeField]
    private SpriteRenderer _sourceRenderer;

    [Tooltip("원본 위에 겹쳐 그릴 실루엣 스프라이트 (흰색 실루엣 머티리얼). 원본의 자식으로 두고 위치 / 크기 0, 1로")]
    [SerializeField]
    private SpriteRenderer _flashRenderer;

    [Header("타이밍")]
    [Tooltip("공격 판정 시작 몇 초 전에 반짝일지")]
    [SerializeField, Min(0f)]
    private float _leadTime = 0.3f;

    [Tooltip("반짝임 전체 시간 (초)")]
    [SerializeField, Min(0.01f)]
    private float _duration = 0.25f;

    [Tooltip("반짝이는 횟수 (전체 시간 안에서 나눠서 깜빡임)")]
    [SerializeField, Min(1)]
    private int _pulseCount = 1;

    [Header("모양")]
    [SerializeField]
    private Color _color = new Color(1f, 0.95f, 0.6f, 1f);

    [SerializeField, Range(0f, 1f)]
    private float _peakOpacity = 0.9f;

    [Tooltip("반짝일 때 함께 생성할 이펙트 (별 반짝임 등, 선택). 스스로 사라지게 만들어 두기")]
    [SerializeField]
    private GameObject _glintEffectPrefab;

    [Tooltip("이펙트 생성 위치 (비우면 원본 스프라이트 중심)")]
    [SerializeField]
    private Transform _glintPoint;

    private const int FlashSortingOffset = 2;

    private float _elapsed;
    private bool _isPlaying;
    private Color _currentColor;

    /// <summary>공격 판정 시작 몇 초 전에 재생해야 하는지</summary>
    public float LeadTime => _leadTime;

    private void Awake()
    {
        if (_sourceRenderer == null)
        {
            foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer != _flashRenderer)
                {
                    _sourceRenderer = renderer;
                    break;
                }
            }
        }

        if (_sourceRenderer == null || _flashRenderer == null)
        {
            Debug.LogWarning($"{name}: ParryWarningFlash의 원본 / Flash Renderer가 없어 반짝임을 사용하지 않습니다.", this);
            enabled = false;
            return;
        }

        _flashRenderer.enabled = false;
    }

    private void OnDisable()
    {
        Stop();
    }

    /// <summary>반짝임 재생 (재생 중이면 처음부터 다시)</summary>
    public void Play()
    {
        Play(_color);
    }

    /// <summary>지정한 색으로 반짝임 재생 (패링 예고 외의 다른 예고에 다른 색을 쓸 때. 예: 피해야 하는 공격은 빨강)</summary>
    public void Play(Color color)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        _currentColor = color;

        _elapsed = 0f;
        _isPlaying = true;
        UpdateFlash();

        if (_glintEffectPrefab != null)
        {
            Vector3 position = _glintPoint != null ? _glintPoint.position : _sourceRenderer.bounds.center;
            Instantiate(_glintEffectPrefab, position, Quaternion.identity);
        }
    }

    public void Stop()
    {
        _isPlaying = false;
        if (_flashRenderer != null)
        {
            _flashRenderer.enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (!_isPlaying)
        {
            return;
        }

        // 공격 타이머와 맞추기 위해 일반 시간 사용 (일시정지 / 히트스탑 중에는 같이 멈춤)
        _elapsed += Time.deltaTime;
        if (_elapsed >= _duration)
        {
            Stop();
            return;
        }

        UpdateFlash();
    }

    private void UpdateFlash()
    {
        _flashRenderer.enabled = _sourceRenderer.enabled;
        if (!_flashRenderer.enabled)
        {
            return;
        }

        _flashRenderer.sprite = _sourceRenderer.sprite;
        _flashRenderer.flipX = _sourceRenderer.flipX;
        _flashRenderer.flipY = _sourceRenderer.flipY;
        _flashRenderer.sortingLayerID = _sourceRenderer.sortingLayerID;
        _flashRenderer.sortingOrder = _sourceRenderer.sortingOrder + FlashSortingOffset;
        _flashRenderer.maskInteraction = _sourceRenderer.maskInteraction;

        // 깜빡임 하나 안에서 빠르게 밝아졌다 천천히 어두워짐
        float pulse = Mathf.Repeat(_elapsed / _duration * _pulseCount, 1f);
        float opacity = pulse < 0.25f
            ? Mathf.SmoothStep(0f, 1f, pulse / 0.25f)
            : Mathf.SmoothStep(1f, 0f, (pulse - 0.25f) / 0.75f);

        Color color = _currentColor;
        color.a *= _sourceRenderer.color.a * _peakOpacity * opacity;
        _flashRenderer.color = color;
    }
}
