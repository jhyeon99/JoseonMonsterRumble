using System.Collections;
using System.Collections.Generic;
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

    protected override void OnPlayerDamaged(HitData hitData)
    {
        AudioManager.Instance?.PlaySceneEvent("Boss.SpitImpact", this);
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

    [Header("중력 느낌 - 세로 속도에 따른 모양 (날아가는 루프 상태에서만)")]
    [Tooltip("켜면 날아가는 동안 세로 속도에 따라 모양이 바뀜 (올라갈 땐 짧고 통통, 떨어질 땐 길고 가늘게)")]
    [SerializeField]
    private bool _useVelocityShape = true;

    [Tooltip("올라가는 속도가 Shape Reference Speed 이상일 때 진행 방향 길이 비율 (1보다 작으면 짧고 통통)")]
    [SerializeField, Range(0.5f, 1.2f)]
    private float _risingLength = 0.9f;

    [Tooltip("떨어지는 속도가 Shape Reference Speed 이상일 때 진행 방향 길이 비율 (1보다 크면 길고 가늘게). 원본이 길쭉하니 작게")]
    [SerializeField, Range(0.8f, 1.8f)]
    private float _fallingLength = 1.2f;

    [Tooltip("이 세로 속도에서 Rising / Falling Length에 도달. 0 ~ 이 속도 사이에서 원본(1) → 최대로 바뀜")]
    [SerializeField, Min(0.1f)]
    private float _shapeReferenceSpeed = 12f;

    [Tooltip("길이가 바뀐 만큼 옆 두께가 반대로 바뀌는 정도. 1이면 부피 유지, 0이면 두께 그대로")]
    [SerializeField, Range(0f, 1f)]
    private float _volumePreserve = 1f;

    [Header("중력 느낌 - 젤리 출렁임")]
    [Tooltip("모양이 목표로 돌아가려는 힘 (클수록 빠르게 출렁임)")]
    [SerializeField, Min(1f)]
    private float _wobbleStiffness = 180f;

    [Tooltip("출렁임이 가라앉는 정도 (작을수록 오래 출렁임. 약 27이면 출렁임 없이 바로 따라감)")]
    [SerializeField, Min(0f)]
    private float _wobbleDamping = 10f;

    [Tooltip("시작(start) 애니메이션이 끝나고 날아가기 시작할 때 주는 출렁임 세기 (0이면 없음)")]
    [SerializeField]
    private float _launchWobbleKick = -2.5f;

    [Header("중력 느낌 - 회전 지연")]
    [Tooltip("켜면 진행 방향으로 즉시 돌지 않고 스프링처럼 늦게 따라 돎 (정점 지나 아래로 꺾일 때 축 처지는 느낌)")]
    [SerializeField]
    private bool _useRotationLag = true;

    [Tooltip("진행 방향으로 돌아가려는 힘 (클수록 빨리 따라감)")]
    [SerializeField, Min(1f)]
    private float _rotationStiffness = 60f;

    [Tooltip("회전 흔들림이 가라앉는 정도 (작을수록 진행 방향을 지나쳤다 돌아옴. 약 15면 지나치지 않음)")]
    [SerializeField, Min(0f)]
    private float _rotationDamping = 12f;

    [Header("중력 느낌 - 잔상 꼬리 (같은 그림을 작고 반투명하게 복제해 지난 위치를 따라옴)")]
    [Tooltip("잔상 개수 (0이면 끔)")]
    [SerializeField, Range(0, 6)]
    private int _ghostCount = 3;

    [Tooltip("잔상 하나당 따라오는 시간 차 (초). 클수록 꼬리가 길게 늘어짐")]
    [SerializeField, Min(0.005f)]
    private float _ghostInterval = 0.035f;

    [Tooltip("첫 잔상 크기 (본체 대비). 뒤로 갈수록 Ghost Scale Falloff만큼 더 작아짐")]
    [SerializeField, Range(0.1f, 1f)]
    private float _ghostScale = 0.8f;

    [SerializeField, Range(0.1f, 1f)]
    private float _ghostScaleFalloff = 0.8f;

    [Tooltip("첫 잔상 투명도. 뒤로 갈수록 Ghost Alpha Falloff만큼 더 투명해짐")]
    [SerializeField, Range(0f, 1f)]
    private float _ghostAlpha = 0.45f;

    [SerializeField, Range(0.1f, 1f)]
    private float _ghostAlphaFalloff = 0.6f;

    [Header("중력 느낌 - 떨어지는 방울 (같은 그림을 아주 작게 흘려 실제 중력으로 떨어짐)")]
    [Tooltip("켜면 날아가는 동안 작은 방울을 흘림")]
    [SerializeField]
    private bool _useDroplets = true;

    [Tooltip("방울을 흘리는 간격 (초, 최소 ~ 최대 사이 무작위)")]
    [SerializeField]
    private Vector2 _dropletInterval = new Vector2(0.06f, 0.14f);

    [Tooltip("방울 크기 (본체 대비, 최소 ~ 최대 사이 무작위)")]
    [SerializeField]
    private Vector2 _dropletScale = new Vector2(0.12f, 0.25f);

    [Tooltip("방울이 사라지기까지 (초)")]
    [SerializeField, Min(0.05f)]
    private float _dropletLifetime = 0.55f;

    [Tooltip("방울이 이어받는 침 속도 비율 (작을수록 흘린 자리에서 바로 떨어짐)")]
    [SerializeField, Range(0f, 1f)]
    private float _dropletInheritVelocity = 0.25f;

    [Tooltip("방울 중력 = 침 중력(Gravity) x 이 값")]
    [SerializeField, Min(0f)]
    private float _dropletGravityMultiplier = 1.5f;

    [Tooltip("방울이 나오는 위치: 본체 중심에서 진행 반대 방향으로 이 거리만큼 뒤")]
    [SerializeField]
    private float _dropletBackOffset = 0.3f;

    [Header("중력 느낌 - 착지 눌림")]
    [Tooltip("바닥 / 벽에 닿는 순간 면 쪽으로 납작하게 눌리는 비율 (0이면 끔). 터지는(end) 애니메이션 위에 잠깐 겹쳐짐")]
    [SerializeField, Range(0f, 0.9f)]
    private float _impactSquash = 0.35f;

    [Tooltip("눌린 모양이 원래대로 돌아오는 시간 (초)")]
    [SerializeField, Min(0.01f)]
    private float _impactSquashDuration = 0.1f;

    [Tooltip("날아가는 루프 애니메이터 상태 이름. 이 상태에서만 모양 변화를 적용 (시작 / 터지는 애니메이션의 크기 키와 겹치지 않도록)")]
    [SerializeField]
    private string _flightStateName = "Boss_Spit";

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

    private Transform _shapeTarget;
    private Vector3 _shapeBaseScale = Vector3.one;
    private Vector3 _shapeWrittenScale = Vector3.one;
    private bool _hasShapeWritten;
    private bool _wasInFlightState;
    private float _shapeLength = 1f;
    private float _shapeLengthVelocity;
    private float _visualAngle;
    private float _visualAngularVelocity;
    private int _flightStateHash;
    private float _impactElapsed = -1f;

    private struct TrailSample
    {
        public float Time;
        public Vector3 Position;
        public float Angle;
        public Vector3 Scale;
    }

    private readonly List<TrailSample> _trailSamples = new List<TrailSample>();
    private SpriteRenderer[] _ghosts;
    private readonly List<BossSpitDroplet> _droplets = new List<BossSpitDroplet>();
    private float _nextDropletTime;

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

        // 모양 변화는 그림(Visual)에만 적용 (루트 콜라이더 / 터지는 위치 계산에 영향 없도록)
        _shapeTarget = _renderer != null && _renderer.transform != transform ? _renderer.transform : null;
        _flightStateHash = string.IsNullOrEmpty(_flightStateName) ? 0 : Animator.StringToHash(_flightStateName);
        CreateGhosts();
    }

    private void CreateGhosts()
    {
        if (_renderer == null || _ghostCount <= 0)
        {
            return;
        }

        _ghosts = new SpriteRenderer[_ghostCount];
        for (int i = 0; i < _ghostCount; i++)
        {
            var ghostObject = new GameObject($"SpitGhost{i}");
            ghostObject.transform.SetParent(transform, false);
            SpriteRenderer ghost = ghostObject.AddComponent<SpriteRenderer>();
            ghost.sharedMaterial = _renderer.sharedMaterial;
            ghost.enabled = false;
            _ghosts[i] = ghost;
        }
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
        ResetShape();

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

        // 발사 순간은 진행 방향으로 바로 맞춤 (이후 Fire에서 늦게 따라감)
        _visualAngle = GetVelocityAngle();
        _visualAngularVelocity = 0f;
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
        UpdateShape();
        UpdateGhosts();
        UpdateDroplets();
        UpdateParryFlash();
    }

    /// <summary>잔상 꼬리: 지난 위치 / 각도 / 크기를 기록해 두고, 시간 차를 두고 작고 투명하게 따라 그림</summary>
    private void UpdateGhosts()
    {
        if (_ghosts == null || _renderer == null)
        {
            return;
        }

        bool moving = (_isLaunching || IsReflected) && !_isEnding && _renderer.enabled;
        if (!moving)
        {
            HideGhosts();
            _trailSamples.Clear();
            return;
        }

        float now = Time.time;
        _trailSamples.Add(new TrailSample
        {
            Time = now,
            Position = _renderer.transform.position,
            Angle = transform.eulerAngles.z,
            Scale = _renderer.transform.lossyScale,
        });

        // 가장 뒤 잔상보다 오래된 기록은 버림 (보간용으로 하나는 남김)
        float oldest = now - (_ghostInterval * (_ghosts.Length + 1));
        while (_trailSamples.Count > 2 && _trailSamples[1].Time < oldest)
        {
            _trailSamples.RemoveAt(0);
        }

        Vector3 rootScale = transform.lossyScale;
        float scale = _ghostScale;
        float alpha = _ghostAlpha;
        for (int i = 0; i < _ghosts.Length; i++)
        {
            SpriteRenderer ghost = _ghosts[i];
            float sampleTime = now - (_ghostInterval * (i + 1));

            // 기록이 아직 그만큼 쌓이지 않았으면 (발사 직후) 숨김
            if (_trailSamples[0].Time > sampleTime || alpha <= 0.001f)
            {
                ghost.enabled = false;
            }
            else
            {
                TrailSample sample = SampleTrail(sampleTime);
                ghost.enabled = true;
                ghost.sprite = _renderer.sprite;
                ghost.flipX = _renderer.flipX;
                ghost.flipY = _renderer.flipY;
                ghost.sortingLayerID = _renderer.sortingLayerID;
                ghost.sortingOrder = _renderer.sortingOrder - 1 - i;

                Color color = _renderer.color;
                color.a *= alpha;
                ghost.color = color;

                Transform ghostTransform = ghost.transform;
                ghostTransform.SetPositionAndRotation(sample.Position, Quaternion.Euler(0f, 0f, sample.Angle));
                ghostTransform.localScale = new Vector3(
                    SafeDivide(sample.Scale.x, rootScale.x) * scale,
                    SafeDivide(sample.Scale.y, rootScale.y) * scale,
                    1f);
            }

            scale *= _ghostScaleFalloff;
            alpha *= _ghostAlphaFalloff;
        }
    }

    private TrailSample SampleTrail(float time)
    {
        for (int i = _trailSamples.Count - 1; i > 0; i--)
        {
            TrailSample older = _trailSamples[i - 1];
            if (older.Time <= time)
            {
                TrailSample newer = _trailSamples[i];
                float t = newer.Time > older.Time ? Mathf.Clamp01((time - older.Time) / (newer.Time - older.Time)) : 1f;
                return new TrailSample
                {
                    Time = time,
                    Position = Vector3.Lerp(older.Position, newer.Position, t),
                    Angle = Mathf.LerpAngle(older.Angle, newer.Angle, t),
                    Scale = Vector3.Lerp(older.Scale, newer.Scale, t),
                };
            }
        }

        return _trailSamples[0];
    }

    private static float SafeDivide(float value, float divisor)
    {
        return Mathf.Abs(divisor) > 0.0001f ? value / divisor : value;
    }

    private void HideGhosts()
    {
        if (_ghosts == null)
        {
            return;
        }

        foreach (SpriteRenderer ghost in _ghosts)
        {
            if (ghost != null)
            {
                ghost.enabled = false;
            }
        }
    }

    /// <summary>날아가는 동안 꼬리 쪽에서 작은 방울을 흘림. 방울은 침과 따로 움직여 침이 사라져도 마저 떨어짐</summary>
    private void UpdateDroplets()
    {
        if (!_useDroplets || _renderer == null || !_isLaunching || _isEnding || IsReflected || !_renderer.enabled)
        {
            return;
        }

        if (Time.time < _nextDropletTime)
        {
            return;
        }

        _nextDropletTime = Time.time + Random.Range(_dropletInterval.x, Mathf.Max(_dropletInterval.x, _dropletInterval.y));
        if (_velocity.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector2 direction = _velocity.normalized;
        Vector3 position = _renderer.transform.position - (Vector3)(direction * _dropletBackOffset);
        Vector2 velocity = (_velocity * _dropletInheritVelocity) + (Random.insideUnitCircle * 0.5f);
        float size = Random.Range(_dropletScale.x, Mathf.Max(_dropletScale.x, _dropletScale.y));
        Vector3 bodyScale = _renderer.transform.lossyScale;

        BossSpitDroplet droplet = GetDroplet();
        droplet.Play(_renderer, position, velocity, _gravity * _dropletGravityMultiplier,
            new Vector3(bodyScale.x * size, bodyScale.y * size, 1f), _dropletLifetime, _spriteFacesLeft);
    }

    private BossSpitDroplet GetDroplet()
    {
        foreach (BossSpitDroplet droplet in _droplets)
        {
            if (droplet != null && !droplet.IsPlaying)
            {
                return droplet;
            }
        }

        var dropletObject = new GameObject("SpitDroplet");
        BossSpitDroplet created = dropletObject.AddComponent<BossSpitDroplet>();
        created.Initialize(_renderer.sharedMaterial);
        _droplets.Add(created);
        return created;
    }

    /// <summary>
    /// 세로 속도에 따라 진행 방향 길이를 바꾸고 (올라갈 땐 짧고 통통, 떨어질 땐 길고 가늘게)
    /// 스프링으로 따라가게 해서 젤리처럼 출렁임. 날아가는 루프 상태에서만 적용
    /// (애니메이터가 Visual 크기를 키로 쓰므로, 애니메이터가 쓴 값에 비율을 곱함)
    /// </summary>
    private void UpdateShape()
    {
        if (_shapeTarget == null)
        {
            return;
        }

        // 착지 눌림: 터지는 애니메이션 위에 잠깐 면 쪽으로 납작하게
        if (_isEnding)
        {
            _wasInFlightState = false;
            if (_impactElapsed < 0f || _impactSquash <= 0f || _impactElapsed >= _impactSquashDuration)
            {
                RestoreShapeScale();
                return;
            }

            _impactElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_impactElapsed / _impactSquashDuration);
            float along = 1f - (_impactSquash * (1f - Mathf.SmoothStep(0f, 1f, t)));
            ApplyShapeScale(along, Mathf.Lerp(1f, 1f / along, _volumePreserve));
            return;
        }

        bool inFlight = _useVelocityShape && _isLaunching && !IsReflected && IsInFlightState();
        if (!inFlight)
        {
            RestoreShapeScale();
            _wasInFlightState = false;
            return;
        }

        // 시작 애니메이션이 끝나고 날아가기 시작하는 순간: 원본 모양에서 출발해 한 번 출렁임
        if (!_wasInFlightState)
        {
            _wasInFlightState = true;
            _shapeLength = 1f;
            _shapeLengthVelocity = _launchWobbleKick;
        }

        float target = GetTargetLength();
        float deltaTime = Mathf.Min(Time.deltaTime, 0.05f);
        float acceleration = (_wobbleStiffness * (target - _shapeLength)) - (_wobbleDamping * _shapeLengthVelocity);
        _shapeLengthVelocity += acceleration * deltaTime;
        _shapeLength = Mathf.Clamp(_shapeLength + (_shapeLengthVelocity * deltaTime), 0.4f, 2f);

        ApplyShapeScale(_shapeLength, Mathf.Lerp(1f, 1f / _shapeLength, _volumePreserve));
    }

    /// <summary>애니메이터가 쓴 크기(이번 프레임에 안 썼으면 원래 값)에 진행 방향 / 옆 비율을 곱해 적용</summary>
    private void ApplyShapeScale(float along, float side)
    {
        Vector3 current = _shapeTarget.localScale;
        if (!_hasShapeWritten || current != _shapeWrittenScale)
        {
            _shapeBaseScale = current;
        }

        var scaled = new Vector3(_shapeBaseScale.x * along, _shapeBaseScale.y * side, _shapeBaseScale.z);
        _shapeTarget.localScale = scaled;
        _shapeWrittenScale = scaled;
        _hasShapeWritten = true;
    }

    /// <summary>세로 속도 → 목표 길이. 올라가는 중 Rising Length, 떨어지는 중 Falling Length 쪽으로</summary>
    private float GetTargetLength()
    {
        float ratio = Mathf.Clamp01(Mathf.Abs(_velocity.y) / _shapeReferenceSpeed);
        return _velocity.y >= 0f
            ? Mathf.Lerp(1f, _risingLength, ratio)
            : Mathf.Lerp(1f, _fallingLength, ratio);
    }

    private bool IsInFlightState()
    {
        if (_animator == null || _flightStateHash == 0 || !_animator.isActiveAndEnabled)
        {
            return true;
        }

        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
        return state.shortNameHash == _flightStateHash && !_animator.IsInTransition(0);
    }

    /// <summary>우리가 바꾼 크기를 원래대로 (애니메이터가 이후 크기를 다시 씀)</summary>
    private void RestoreShapeScale()
    {
        if (_hasShapeWritten && _shapeTarget != null && _shapeTarget.localScale == _shapeWrittenScale)
        {
            _shapeTarget.localScale = _shapeBaseScale;
        }

        _hasShapeWritten = false;
    }

    private void ResetShape()
    {
        RestoreShapeScale();
        _wasInFlightState = false;
        _shapeLength = 1f;
        _shapeLengthVelocity = 0f;
        _visualAngularVelocity = 0f;
        _impactElapsed = -1f;
        _trailSamples.Clear();
        HideGhosts();
        _nextDropletTime = 0f;
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
        UpdateLaggedAngle(deltaTime);
        UpdateRotation();
    }

    /// <summary>진행 방향 각도 (Sprite Faces Left 반영)</summary>
    private float GetVelocityAngle()
    {
        if (_velocity.sqrMagnitude < 0.0001f)
        {
            return _visualAngle;
        }

        float angle = Mathf.Atan2(_velocity.y, _velocity.x) * Mathf.Rad2Deg;
        return _spriteFacesLeft ? angle + 180f : angle;
    }

    /// <summary>회전 지연: 진행 방향을 스프링처럼 늦게 따라감. 끄면 바로 맞춤</summary>
    private void UpdateLaggedAngle(float deltaTime)
    {
        float target = GetVelocityAngle();
        if (!_useRotationLag)
        {
            _visualAngle = target;
            _visualAngularVelocity = 0f;
            return;
        }

        deltaTime = Mathf.Min(deltaTime, 0.05f);
        float difference = Mathf.DeltaAngle(_visualAngle, target);
        float angularAcceleration = (_rotationStiffness * difference) - (_rotationDamping * _visualAngularVelocity);
        _visualAngularVelocity += angularAcceleration * deltaTime;
        _visualAngle += _visualAngularVelocity * deltaTime;
    }

    private void UpdateRotation()
    {
        if (!_rotateToVelocity || _velocity.sqrMagnitude < 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.Euler(0f, 0f, _visualAngle);
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

        // 착지 눌림 시작 (UpdateShape)
        _impactElapsed = 0f;

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

    private void OnDestroy()
    {
        foreach (BossSpitDroplet droplet in _droplets)
        {
            if (droplet != null)
            {
                Destroy(droplet.gameObject);
            }
        }

        _droplets.Clear();
    }

    private void OnDisable()
    {
        _velocity = Vector2.zero;
        _isLaunching = false;
        _isEnding = false;
        _endRoutine = null;
        _isParryable = false;
        RestoreRendererColor();
        ResetShape();

        // 다음에 풀에서 꺼낼 때 맞을 수 있도록
        if (_collider != null)
        {
            _collider.enabled = true;
        }
    }
}