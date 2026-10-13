using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 침뱉기 투사체. 중력을 받아 포물선으로 날아감 (발사 속도는 BossSpitPattern이 Launch로 지정).
/// 애니메이션: 발사 시 시작(start) → 날아가는 동안 반복(loop) → 바닥 / 벽에 닿으면 터지는(end) 애니메이션 후 사라짐.
/// 플레이어에 맞으면 바로 사라짐 (Projectile 기본 처리).
/// 발사할 때 패링 가능 여부가 정해짐 (BossSpitPattern). 패링 가능한 침은 날아가는 동안 반짝이고,
/// 패링하면 그 순간 보스가 있던 위치로 직진. 패링 불가 침은 패링 타이밍에 막으면 게이지 소모 없이 막기만.
/// 프리팹의 Type을 BossSpit으로 두고 ProjectilePool의 투사체 프리팹 목록에 등록해서 사용.
/// </summary>
public class BossSpitProjectile : Projectile
{
    [Header("독")]
    [Tooltip("맞으면 걸리는 독 지속 시간 (초). 0이면 독 없음")]
    [SerializeField, Min(0f)]
    private float _poisonDuration = 10f;

    protected override void ModifyHitData(ref HitData hitData)
    {
        hitData.PoisonDuration = _poisonDuration;
    }

    [Header("포물선")]
    [Tooltip("아래로 당기는 가속도 (클수록 빨리 떨어지고 궤적이 둥글게 휨)")]
    [SerializeField, Min(0.01f)]
    private float _gravity = 8f;

    [Tooltip("날아가는 방향으로 스프라이트를 돌림")]
    [SerializeField]
    private bool _rotateToVelocity = true;

    [Tooltip("스프라이트 원본의 앞쪽이 왼쪽이면 체크 (적 투사체 기본). 오른쪽이면 끄기")]
    [SerializeField]
    private bool _spriteFacesLeft = true;

    [Header("패링 가능 표시 (보스 / 지상 근거리 적의 패링 예고와 같은 노란 반짝임)")]
    [Tooltip("반짝임 실루엣 머티리얼 (보스 / 적 ParryFlash와 같은 흰색 실루엣 머티리얼). 비우면 스프라이트 색만 깜빡임")]
    [SerializeField]
    private Material _parryFlashMaterial;

    [Tooltip("반짝임 색 (보스 / 적 패링 예고와 같은 값)")]
    [SerializeField]
    private Color _parryFlashColor = new Color(1f, 0.95f, 0.6f, 1f);

    [SerializeField, Range(0f, 1f)]
    private float _parryFlashPeakOpacity = 0.9f;

    [Tooltip("반짝임 한 번의 시간 (초)")]
    [SerializeField, Min(0.01f)]
    private float _parryFlashDuration = 0.25f;

    [Tooltip("반짝임을 반복하는 간격 (초). 날아가는 동안 계속 반복")]
    [SerializeField, Min(0.05f)]
    private float _parryFlashInterval = 0.35f;

    [Header("땅에 닿았을 때 (end 애니메이션)")]
    [Tooltip("땅에 닿으면 켤 애니메이터 Bool (loop → end 전이 조건)")]
    [SerializeField]
    private string _endBool = "isEnd";

    [Tooltip("end 애니메이션 클립. 이 길이만큼 재생한 뒤 사라짐. 비우면 애니메이터에서 이름에 End가 들어간 클립을 찾음")]
    [SerializeField]
    private AnimationClip _endClip;

    [Tooltip("end 클립을 찾지 못했을 때 사라지기까지 시간 (초)")]
    [SerializeField, Min(0f)]
    private float _endFallbackDuration = 0.5f;

    [Tooltip("터질 때 부딪힌 면에 맞춰 회전 (바닥이면 터지는 면이 아래로). 끄면 날아오던 각도 그대로")]
    [SerializeField]
    private bool _resetRotationOnEnd = true;

    [Tooltip("터지는 위치를 부딪힌 면 기준으로 밀어냄. 음수 = 면 안쪽(바닥이면 아래로), 양수 = 바깥쪽(위로)")]
    [SerializeField]
    private float _endSurfaceOffset = 1.0f;

    [Tooltip("end 그림에서 표면에 닿는 지점 (프리팹 기준 로컬 위치, 회전 전). 이 점이 부딪힌 지점에 붙음. (0, 0)이면 프리팹 중심. 선택하면 노란 점으로 표시")]
    [SerializeField]
    private Vector2 _endImpactPoint = Vector2.zero;

    private Vector2 _velocity;
    private Animator _animator;
    private Collider2D _collider;
    private bool _isEnding;
    private Coroutine _endRoutine;
    private bool _isParryable;
    private float _parryFlashElapsed;
    private SpriteRenderer _renderer;
    private SpriteRenderer _flashRenderer;
    private Color _rendererBaseColor = Color.white;

    private const int FlashSortingOffset = 2;

    /// <summary>패턴이 발사 속도를 계산할 때 쓰는 중력</summary>
    public float Gravity => _gravity;

    protected override Vector2 HitDirection =>
        IsReflected ? ReflectDirection : (_velocity.sqrMagnitude > 0.0001f ? _velocity.normalized : Vector2.down);

    protected override void Awake()
    {
        base.Awake();
        _animator = GetComponentInChildren<Animator>();
        _collider = GetComponent<Collider2D>();
        _renderer = GetComponentInChildren<SpriteRenderer>(true);
        if (_renderer != null)
        {
            _rendererBaseColor = _renderer.color;
            CreateFlashRenderer();
        }

        EnsureKinematicBody();
    }

    /// <summary>
    /// 트리거 충돌 이벤트는 둘 중 하나에 Rigidbody2D가 있어야 발생함.
    /// 바닥 / 벽(Rigidbody 없는 정적 콜라이더)에 닿은 것을 감지하려면 침에 Rigidbody2D가 필요해서, 없으면 Kinematic으로 추가
    /// (이동은 계속 코드로 직접 함)
    /// </summary>
    private void EnsureKinematicBody()
    {
        if (!TryGetComponent(out Rigidbody2D body))
        {
            body = gameObject.AddComponent<Rigidbody2D>();
        }

        body.bodyType = RigidbodyType2D.Kinematic;
        body.useFullKinematicContacts = true;
        body.gravityScale = 0f;
        body.linearVelocity = Vector2.zero;
    }

    public override void OnSpawn(Transform muzzlePoint, Transform playerPoint, float damageIncreaseValue)
    {
        base.OnSpawn(muzzlePoint, playerPoint, damageIncreaseValue);
        transform.position = muzzlePoint.position;
        ResetEnd();

        // 풀에서 다시 꺼낼 때 시작(start) 애니메이션부터
        if (_animator != null)
        {
            _animator.Rebind();
            _animator.Update(0f);
        }

        // 패턴이 Launch를 따로 부르지 않으면 플레이어 위치로 1초 동안 날아가는 포물선
        Vector2 target = playerPoint != null ? (Vector2)playerPoint.position : MuzzlePosition + (Vector2.left * 5f);
        Launch(CalculateVelocity(MuzzlePosition, target, 1f, _gravity));
    }

    /// <summary>
    /// 이번 발사의 패링 가능 여부. 패링 가능하면 날아가는 동안 반짝임,
    /// 불가면 패링 타이밍에 막을 때 게이지 소모 없이 막기만 (프리팹 Defense 기준으로 덮어씀)
    /// </summary>
    public void SetParryable(bool parryable)
    {
        AttackDefense defense = BaseDefense;
        defense.Parryable = parryable;
        defense.FreeBlockOnParryTiming = !parryable;
        SetDefenseOverride(defense);

        _isParryable = parryable;
        _parryFlashElapsed = 0f;
        RestoreRendererColor();
    }

    /// <summary>원본 스프라이트 위에 겹쳐 그릴 흰 실루엣 (ParryWarningFlash와 같은 방식). 원본 자식이라 애니메이션 / 회전을 그대로 따라감</summary>
    private void CreateFlashRenderer()
    {
        if (_parryFlashMaterial == null)
        {
            return;
        }

        var flashObject = new GameObject("ParryFlash");
        flashObject.transform.SetParent(_renderer.transform, false);
        _flashRenderer = flashObject.AddComponent<SpriteRenderer>();
        _flashRenderer.sharedMaterial = _parryFlashMaterial;
        _flashRenderer.enabled = false;
    }

    /// <summary>발사 속도 지정 (위치는 그대로)</summary>
    public void Launch(Vector2 velocity)
    {
        _velocity = velocity;
        _isLaunching = true;
        UpdateRotation();
    }

    /// <summary>from에서 출발해 flightTime초 뒤 to를 지나는 발사 속도</summary>
    public static Vector2 CalculateVelocity(Vector2 from, Vector2 to, float flightTime, float gravity)
    {
        float time = Mathf.Max(0.05f, flightTime);
        Vector2 offset = to - from;
        return new Vector2(offset.x / time, (offset.y / time) + (0.5f * gravity * time));
    }

    // 애니메이터가 스프라이트를 바꾼 뒤에 따라 그리도록 LateUpdate
    private void LateUpdate()
    {
        UpdateParryFlash();
    }

    /// <summary>
    /// 패링 가능한 침은 날아가는 동안 Interval마다 노랗게 반짝임 (반사 / 터지는 중에는 끔).
    /// 반짝임 모양은 ParryWarningFlash와 같음: 빠르게 밝아졌다가 천천히 어두워짐
    /// </summary>
    private void UpdateParryFlash()
    {
        if (!_isParryable || IsReflected || _isEnding || _renderer == null)
        {
            return;
        }

        _parryFlashElapsed += Time.deltaTime;
        float interval = Mathf.Max(_parryFlashInterval, _parryFlashDuration);
        float t = Mathf.Repeat(_parryFlashElapsed, interval);

        float opacity = 0f;
        if (t < _parryFlashDuration)
        {
            float pulse = t / _parryFlashDuration;
            opacity = pulse < 0.25f
                ? Mathf.SmoothStep(0f, 1f, pulse / 0.25f)
                : Mathf.SmoothStep(1f, 0f, (pulse - 0.25f) / 0.75f);
        }

        if (_flashRenderer != null)
        {
            _flashRenderer.enabled = _renderer.enabled && opacity > 0f;
            if (!_flashRenderer.enabled)
            {
                return;
            }

            _flashRenderer.sprite = _renderer.sprite;
            _flashRenderer.flipX = _renderer.flipX;
            _flashRenderer.flipY = _renderer.flipY;
            _flashRenderer.sortingLayerID = _renderer.sortingLayerID;
            _flashRenderer.sortingOrder = _renderer.sortingOrder + FlashSortingOffset;

            Color color = _parryFlashColor;
            color.a *= _renderer.color.a * _parryFlashPeakOpacity * opacity;
            _flashRenderer.color = color;
            return;
        }

        // 실루엣 머티리얼이 없으면 스프라이트 색을 노랗게 깜빡임
        _renderer.color = Color.Lerp(_rendererBaseColor, _parryFlashColor, opacity * _parryFlashPeakOpacity);
    }

    private void RestoreRendererColor()
    {
        if (_renderer != null)
        {
            _renderer.color = _rendererBaseColor;
        }

        if (_flashRenderer != null)
        {
            _flashRenderer.enabled = false;
        }
    }

    protected override void Fire()
    {
        if (!_isLaunching || _isEnding)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        Vector2 acceleration = Vector2.down * _gravity;
        transform.position += (Vector3)((_velocity * deltaTime) + (acceleration * (0.5f * deltaTime * deltaTime)));
        _velocity += acceleration * deltaTime;
        UpdateRotation();
    }

    private void UpdateRotation()
    {
        if (!_rotateToVelocity || _velocity.sqrMagnitude < 0.0001f)
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

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (_isEnding)
        {
            return;
        }

        // 반사된 침은 기존 처리 (적에게 맞거나 벽에 닿으면 사라짐)
        if (IsReflected)
        {
            base.OnTriggerEnter2D(other);
            return;
        }

        int layer = other.gameObject.layer;
        if ((Constants.Layers.GroundAllMask & (1 << layer)) != 0)
        {
            // 트리거 / 아래에서 위로 올라가며 지나치는 일방 발판은 무시
            if (other.isTrigger || (layer == Constants.Layers.OneWayPlatform && _velocity.y > 0f))
            {
                return;
            }

            BeginEnd(other);
            return;
        }

        base.OnTriggerEnter2D(other);
    }

    /// <summary>땅에 닿음: 멈추고 판정을 끈 뒤 end 애니메이션 → 사라짐</summary>
    private void BeginEnd(Collider2D ground)
    {
        RestoreRendererColor();
        Vector2 incoming = _velocity;
        _isEnding = true;
        _isLaunching = false;
        _velocity = Vector2.zero;

        if (_collider != null)
        {
            _collider.enabled = false;
        }

        // 부딪힌 면의 바깥 방향 (바닥이면 위). 이미 콜라이더 안이면 날아오던 방향의 반대로
        Vector2 position = transform.position;
        Vector2 contact = ground.ClosestPoint(position);
        Vector2 normal = position - contact;
        if (normal.sqrMagnitude < 0.0001f)
        {
            normal = incoming.sqrMagnitude > 0.0001f ? -incoming : Vector2.up;
        }

        // 회전한 뒤, 그림의 닿는 지점(End Impact Point)을 표면에 붙이고 End Surface Offset만큼 면 안쪽 / 바깥쪽으로 이동
        if (_resetRotationOnEnd)
        {
            FaceImpactSurface(normal.normalized);
        }

        Vector2 endPosition = contact - GetWorldImpactOffset() + (normal.normalized * _endSurfaceOffset);
        transform.position = new Vector3(endPosition.x, endPosition.y, transform.position.z);

        if (_animator != null && !string.IsNullOrEmpty(_endBool))
        {
            _animator.SetBool(_endBool, true);
        }

        // 꺼지는 중인 같은 프레임에 충돌이 들어오면 코루틴을 못 돌리므로 바로 정리
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(false);
            return;
        }

        _endRoutine = StartCoroutine(DisableAfter(GetEndDuration()));
    }

    /// <summary>
    /// 터지는 스프라이트의 평평한 면(진행 방향 앞쪽)이 부딪힌 면을 향하도록 회전.
    /// 앞쪽은 Sprite Faces Left 기준 (켜면 왼쪽, 끄면 오른쪽). 바닥이면 앞쪽이 아래를 봄
    /// </summary>
    private void FaceImpactSurface(Vector2 surfaceNormal)
    {
        float intoSurfaceAngle = Mathf.Atan2(-surfaceNormal.y, -surfaceNormal.x) * Mathf.Rad2Deg;
        float frontAngle = _spriteFacesLeft ? 180f : 0f;
        transform.rotation = Quaternion.Euler(0f, 0f, intoSurfaceAngle - frontAngle);
    }

    /// <summary>End Impact Point의 현재 회전 / 크기 기준 월드 오프셋</summary>
    private Vector2 GetWorldImpactOffset()
    {
        var scaled = Vector3.Scale(_endImpactPoint, transform.lossyScale);
        return transform.rotation * scaled;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + (Vector3)GetWorldImpactOffset(), 0.15f);
    }

    private IEnumerator DisableAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        _endRoutine = null;
        gameObject.SetActive(false);
    }

    private float GetEndDuration()
    {
        AnimationClip clip = _endClip;
        if (clip == null && _animator != null && _animator.runtimeAnimatorController != null)
        {
            foreach (AnimationClip candidate in _animator.runtimeAnimatorController.animationClips)
            {
                if (candidate != null && candidate.name.ToLower().Contains("end"))
                {
                    clip = candidate;
                    break;
                }
            }
        }

        if (clip == null)
        {
            return _endFallbackDuration;
        }

        float speed = _animator != null && _animator.speed > 0f ? _animator.speed : 1f;
        return clip.length / speed;
    }

    private void ResetEnd()
    {
        if (_endRoutine != null)
        {
            StopCoroutine(_endRoutine);
            _endRoutine = null;
        }

        _isEnding = false;
        if (_collider != null)
        {
            _collider.enabled = true;
        }

        if (_animator != null && !string.IsNullOrEmpty(_endBool))
        {
            _animator.SetBool(_endBool, false);
        }
    }

    private void OnDisable()
    {
        _velocity = Vector2.zero;
        _isLaunching = false;
        _isEnding = false;
        _endRoutine = null;
        _isParryable = false;
        RestoreRendererColor();

        // 다음에 풀에서 꺼낼 때 맞을 수 있도록
        if (_collider != null)
        {
            _collider.enabled = true;
        }
    }
}