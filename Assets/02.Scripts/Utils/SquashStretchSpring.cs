using UnityEngine;

/// <summary>
/// 맞았을 때 스프링처럼 눌렸다가 출렁이며 돌아오는 연출 (squash & stretch).
/// 같은 오브젝트에 EnemyHealth가 있으면 피격 시 자동 재생. 다른 곳에서는 Play()로 직접 호출.
/// 좌우 반전(localScale 부호)은 유지하고 크기만 바꾸므로 EnemyFlip과 같이 써도 됨.
/// </summary>
public class SquashStretchSpring : MonoBehaviour
{
    [Tooltip("크기를 바꿀 대상. 비우면 자식의 SpriteRenderer (콜라이더에 영향이 없도록 스프라이트 자식 권장)")]
    [SerializeField] private Transform _target;

    [Header("스프링")]
    [Tooltip("맞는 순간 가로로 눌리는 정도 (0.3이면 가로 70%, 세로 130%)")]
    [SerializeField, Range(0f, 0.8f)] private float _squashAmount = 0.3f;

    [Tooltip("복원력. 클수록 빨리 돌아오고 빠르게 떨림")]
    [SerializeField, Min(1f)] private float _stiffness = 300f;

    [Tooltip("감쇠. 작을수록 여러 번 출렁임")]
    [SerializeField, Min(0f)] private float _damping = 12f;

    private const float MaxDeltaTime = 0.05f;   // 프레임이 튀어도 스프링이 발산하지 않도록
    private const float RestThreshold = 0.001f;

    private EnemyHealth _enemyHealth;
    private Vector3 _baseScale;   // 반전 부호를 뺀 원래 크기
    private float _offset;        // 0 = 원래 모양, 양수 = 가로로 눌림
    private float _velocity;
    private bool _isPlaying;

    private void Awake()
    {
        if (_target == null)
        {
            SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            _target = spriteRenderer != null ? spriteRenderer.transform : transform;
        }

        Vector3 scale = _target.localScale;
        _baseScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), scale.z);

        _enemyHealth = GetComponent<EnemyHealth>();
    }

    private void OnEnable()
    {
        if (_enemyHealth != null)
        {
            _enemyHealth.OnHit += HandleHit;
        }
    }

    private void OnDisable()
    {
        if (_enemyHealth != null)
        {
            _enemyHealth.OnHit -= HandleHit;
        }

        // 연출 도중 꺼져도 원래 크기로 복구
        _offset = 0f;
        _velocity = 0f;
        _isPlaying = false;
        ApplyScale();
    }

    private void HandleHit(HitData hitData)
    {
        Play();
    }

    /// <summary>squash & stretch 재생. strength로 세기 조절 (1 = 기본)</summary>
    public void Play(float strength = 1f)
    {
        _offset = _squashAmount * strength;
        _velocity = 0f;
        _isPlaying = true;
        ApplyScale();
    }

    // EnemyFlip이 Update에서 부호를 바꾼 뒤에 크기를 적용
    private void LateUpdate()
    {
        if (!_isPlaying) return;

        float deltaTime = Mathf.Min(Time.deltaTime, MaxDeltaTime);

        // 감쇠 스프링: 원래 모양(0)으로 당기는 힘 - 속도에 비례한 감쇠
        float acceleration = -_stiffness * _offset - _damping * _velocity;
        _velocity += acceleration * deltaTime;
        _offset += _velocity * deltaTime;

        if (Mathf.Abs(_offset) < RestThreshold && Mathf.Abs(_velocity) < RestThreshold * 10f)
        {
            _offset = 0f;
            _velocity = 0f;
            _isPlaying = false;
        }

        ApplyScale();
    }

    private void ApplyScale()
    {
        if (_target == null) return;

        // 가로가 줄면 세로가 늘어나서 부피감 유지
        Vector3 scale = _target.localScale;
        scale.x = Mathf.Sign(scale.x) * _baseScale.x * (1f - _offset);
        scale.y = Mathf.Sign(scale.y) * _baseScale.y * (1f + _offset);
        _target.localScale = scale;
    }
}
