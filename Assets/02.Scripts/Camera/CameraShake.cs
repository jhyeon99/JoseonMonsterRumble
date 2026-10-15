using Com.LuisPedroFonseca.ProCamera2D;
using UnityEngine;

/// <summary>
/// 카메라 연출 (흔들림 / 밀기 / 줌). 메인 카메라에 하나만 둠.
/// - GameEvents의 플레이어 피격 / 패링 / 마무리 공격에 자동으로 반응
/// - 그 외(보스 패턴 등)에서는 CameraShake.Shake / Kick / Zoom 으로 호출
/// - 시간은 실제 시간 기준이라 히트스톱 중에도 움직임 (일시정지 중에는 멈춤)
/// - 겹치면 흔들림 / 줌은 더 강한 쪽을 유지, 밀기는 마지막 것으로 교체
/// CameraFollow가 위치를 정한 뒤(실행 순서 10) 오프셋과 줌만 얹음.
/// </summary>
[DefaultExecutionOrder(10)]
public class CameraShake : MonoBehaviour
{
    private static CameraShake _instance;

    [Header("플레이어 피격")]
    [Tooltip("Magnitude는 최대 세기. 실제 세기 = 데미지 x 아래 배율 (최대값까지)")]
    [SerializeField]
    private CameraShakeSettings _playerHitShake = new CameraShakeSettings(0.1f, 0.3f);

    [Tooltip("피격 데미지 1당 흔들림 세기")]
    [SerializeField, Min(0f)]
    private float _damageMultiplier = 0.02f;

    [Tooltip("피격 시 카메라 밀림 설정 (밀리는 거리, 총 시간, 최고점 도달 비율)")]
    [SerializeField]
    private CameraKickSettings _playerHitKick = new CameraKickSettings(0.18f, 0.12f, 0.25f);

    [Header("적 피격")]
    [SerializeField]
    private CameraShakeSettings _enemyLightHitShake = new CameraShakeSettings(0.092f, 0.046f);

    [SerializeField]
    private CameraShakeSettings _enemyHeavyHitShake = new CameraShakeSettings(0.11f, 0.055f);

    [SerializeField]
    private CameraShakeSettings _talismanHitShake = new CameraShakeSettings(0.05f, 0.015f);

    [Header("패링")]
    [SerializeField]
    private CameraShakeSettings _parryShake = new CameraShakeSettings(0.12f, 0.12f);

    [SerializeField]
    private CameraZoomSettings _parryZoom = new CameraZoomSettings(0.06f, 0.03f, 0.1f, 0.25f, 0.15f, 1.5f);

    [Header("마무리 공격")]
    [Tooltip("타격 순간 타격 지점 쪽으로 줌 인")]
    [SerializeField]
    private CameraZoomSettings _finisherZoom = new CameraZoomSettings(0.15f, 0.035f, 0.14f, 0.32f, 0.35f, 2.5f);

    [Tooltip("적이 날아가는 방향으로 카메라가 밀림")]
    [SerializeField]
    private CameraKickSettings _finisherKick = new CameraKickSettings(0.35f, 0.32f, 0.18f);

    [Tooltip("타격감용 약한 흔들림")]
    [SerializeField]
    private CameraShakeSettings _finisherShake = new CameraShakeSettings(0.12f, 0.09f);

    [Header("공통")]
    [Tooltip("세로 흔들림 비율 (멀미 방지)")]
    [SerializeField, Range(0f, 1f)]
    private float _verticalShakeRatio = 0.3f;

    private ProCamera2D _proCamera;
    private ProCamera2DShake _proCameraShake;
    private Camera _camera;
    private CameraFollow _follow;
    private float _baseOrthographicSize;
    private bool _isZoomApplied;
    private Vector3 _lastOffset;

    // 일시정지 중에는 멈추는 실제 시간
    private float _clock;

    // 흔들림
    private float _shakeStartTime;
    private CameraShakeSettings _shake;

    // 밀기
    private float _kickStartTime;
    private CameraKickSettings _kick;
    private Vector2 _kickDirection;

    // 줌
    private float _zoomStartTime;
    private CameraZoomSettings _zoom;
    private Vector2 _focusVector;
    private bool _isZooming;

    private void Awake()
    {
        _proCamera = GetComponent<ProCamera2D>();
        _proCameraShake = GetComponent<ProCamera2DShake>();

        _camera = GetComponent<Camera>();
        if (_camera == null)
        {
            _camera = Camera.main;
        }

        _follow = GetComponent<CameraFollow>();

        if (_camera != null)
        {
            _baseOrthographicSize = _camera.orthographicSize;
        }
    }

    private void OnEnable()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning($"{name}: CameraShake가 씬에 여러 개 있습니다. 메인 카메라에 하나만 남겨 주세요.", this);
        }

        _instance = this;

        GameEvents.OnPlayerHit += HandlePlayerHit;
        GameEvents.OnEnemyHit += HandleEnemyHit;
        GameEvents.OnParry += HandleParry;
        GameEvents.OnFinisher += HandleFinisher;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerHit -= HandlePlayerHit;
        GameEvents.OnEnemyHit -= HandleEnemyHit;
        GameEvents.OnParry -= HandleParry;
        GameEvents.OnFinisher -= HandleFinisher;

        StopAll();

        if (_instance == this)
        {
            _instance = null;
        }
    }

    #region 외부 호출용

    /// <summary>점점 약해지는 흔들림</summary>
    public static void Shake(CameraShakeSettings settings)
    {
        if (_instance != null)
        {
            _instance.StartShake(settings);
        }
    }

    public static void Shake(float duration, float magnitude)
    {
        Shake(new CameraShakeSettings(duration, magnitude));
    }

    /// <summary>direction 쪽으로 밀었다가 돌아옴</summary>
    public static void Kick(Vector2 direction, CameraKickSettings settings)
    {
        if (_instance != null)
        {
            _instance.StartKick(direction, settings);
        }
    }

    /// <summary>제자리 줌</summary>
    public static void Zoom(CameraZoomSettings settings)
    {
        if (_instance != null)
        {
            _instance.StartZoom(settings, null);
        }
    }

    /// <summary>focusPoint 쪽으로 이동하면서 줌</summary>
    public static void Zoom(CameraZoomSettings settings, Vector2 focusPoint)
    {
        if (_instance != null)
        {
            _instance.StartZoom(settings, focusPoint);
        }
    }

    /// <summary>모든 카메라 연출을 바로 끝냄</summary>
    public static void Stop()
    {
        if (_instance != null)
        {
            _instance.StopAll();
        }
    }

    #endregion

    #region 이벤트 반응

    private void HandlePlayerHit(HitData hitData)
    {
        float damageScaled = hitData.Damage * _damageMultiplier;
        float baseMagnitude = _playerHitShake.Magnitude > 0f ? _playerHitShake.Magnitude : 0.5f;
        float magnitude = Mathf.Max(0.35f, Mathf.Min(damageScaled, baseMagnitude));
        float duration = _playerHitShake.Duration > 0f ? _playerHitShake.Duration : 0.18f;

        StartShake(new CameraShakeSettings(duration, magnitude), ignoreTimeScale: true);

        float directionX = 0f;
        if (Mathf.Abs(hitData.AttackDirection.x) > 0.01f)
        {
            directionX = Mathf.Sign(hitData.AttackDirection.x);
        }
        else
        {
            Transform player = _follow != null ? _follow.Target : null;
            if (player != null)
            {
                float diff = player.position.x - hitData.HitPoint.x;
                directionX = Mathf.Abs(diff) > 0.01f ? Mathf.Sign(diff) : -Mathf.Sign(player.localScale.x);
            }
        }

        CameraKickSettings kickSettings = _playerHitKick;
        if (kickSettings.Distance <= 0.001f)
        {
            kickSettings = new CameraKickSettings(0.55f, 0.16f, 0.22f);
        }

        if (Mathf.Abs(directionX) > 0.01f && kickSettings.Distance > 0f)
        {
            StartKick(new Vector2(directionX, 0f), kickSettings);
        }

        // 타격 지점 방향 순간 줌인 펀치로 타격감 극대화
        StartZoom(new CameraZoomSettings(0.05f, 0.02f, 0.04f, 0.12f, 0.15f, 1.2f), hitData.HitPoint);
    }

    private void HandleEnemyHit(HitData hitData)
    {
        CameraShakeSettings settings = hitData.IsTalisman
            ? _talismanHitShake
            : hitData.IsHeavyImpact ? _enemyHeavyHitShake : _enemyLightHitShake;
        StartShake(settings);
    }

    private void HandleParry(HitData hitData)
    {
        // 패링 히트스톱 중에도 성공 순간부터 실제 시간 기준으로 흔든다.
        StartShake(_parryShake, ignoreTimeScale: true);
        StartZoom(_parryZoom, hitData.HitPoint);
    }

    private void HandleFinisher(Vector2 point)
    {
        StartZoom(_finisherZoom, point);
        StartShake(_finisherShake, ignoreTimeScale: true);

        // 날아가는 방향 = 플레이어 → 타격 지점
        float directionX = 1f;
        Transform player = _follow != null ? _follow.Target : null;
        if (player != null)
        {
            float diff = point.x - player.position.x;
            directionX = Mathf.Abs(diff) > 0.01f ? Mathf.Sign(diff) : Mathf.Sign(player.localScale.x);
        }

        StartKick(new Vector2(directionX, 0f), _finisherKick);
    }

    #endregion

    #region 시작

    private void StartShake(CameraShakeSettings settings, bool ignoreTimeScale = false)
    {
        if (settings.Duration <= 0f || settings.Magnitude <= 0f)
        {
            return;
        }

        // 지금 남은 흔들림보다 약하면 무시 (강한 흔들림을 약한 것이 끊지 않도록)
        if (settings.Magnitude < GetCurrentShakeStrength())
        {
            return;
        }

        _shake = settings;
        _shakeStartTime = _clock;

        if (_proCameraShake != null && _proCameraShake.isActiveAndEnabled)
        {
            _proCameraShake.StopShaking();
            _proCameraShake.Shake(settings.Duration,
                new Vector2(settings.Magnitude, settings.Magnitude * _verticalShakeRatio),
                smoothness: 0.03f, ignoreTimeScale: ignoreTimeScale);
        }
    }

    private void StartKick(Vector2 direction, CameraKickSettings settings)
    {
        if (settings.Distance <= 0f || direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        _kick = settings;
        _kickDirection = direction.normalized;
        _kickStartTime = _clock;
    }

    private void StartZoom(CameraZoomSettings settings, Vector2? focusPoint)
    {
        if (Mathf.Approximately(settings.Amount, 0f) || settings.TotalTime <= 0f)
        {
            return;
        }

        // 지금 줌보다 약하면 무시
        if (_isZooming && Mathf.Abs(settings.Amount) < Mathf.Abs(_zoom.Amount * GetZoomWeight()))
        {
            return;
        }

        _zoom = settings;
        _zoomStartTime = _clock;
        _isZooming = true;
        _focusVector = Vector2.zero;

        if (focusPoint.HasValue && settings.FocusStrength > 0f)
        {
            Vector2 basePosition = transform.position - _lastOffset;
            Vector2 toFocus = (focusPoint.Value - basePosition) * settings.FocusStrength;
            _focusVector = Vector2.ClampMagnitude(toFocus, settings.MaxFocusDistance);
        }
    }

    private void StopAll()
    {
        if (_proCameraShake != null && _proCameraShake.isActiveAndEnabled)
            _proCameraShake.StopShaking();

        _shake = default;
        _kick = default;
        _isZooming = false;

        if (_follow == null || !_follow.isActiveAndEnabled)
        {
            transform.position -= _lastOffset;
        }

        _lastOffset = Vector3.zero;
        RestoreZoom();
    }

    #endregion

    private void Update()
    {
        if (PauseController.IsPaused || _proCamera == null || !_proCamera.isActiveAndEnabled)
            return;

        // ProCamera2D may skip Move during hit stop. Remove our previous offset
        // before its LateUpdate so tracking and boundaries never consume it.
        transform.position -= _lastOffset;
        _lastOffset = Vector3.zero;
    }

    private void LateUpdate()
    {
        // ProCamera2D also skips movement while paused, so reapplying an offset
        // here would accumulate it and move the camera away from frozen souls.
        if (PauseController.IsPaused) return;
        _clock += Time.unscaledDeltaTime;

        // ProCamera2D의 이전 오프셋은 Update에서 제거했다.
        // CameraFollow만 매 프레임 자체 기준 위치를 새로 쓴다.
        if ((_follow == null || !_follow.isActiveAndEnabled) &&
            (_proCamera == null || !_proCamera.isActiveAndEnabled))
        {
            transform.position -= _lastOffset;
        }

        float zoomWeight = GetZoomWeight();
        Vector3 offset = GetShakeOffset() + GetKickOffset() + (Vector3)(_focusVector * zoomWeight);

        ApplyZoom(zoomWeight);
        Vector3 basePosition = transform.position;
        transform.position = _follow != null && _follow.isActiveAndEnabled
            ? _follow.ConstrainCameraEffectPosition(basePosition, offset, _camera)
            : basePosition + offset;
        _lastOffset = transform.position - basePosition;
    }

    #region 계산

    private float GetCurrentShakeStrength()
    {
        if (_shake.Duration <= 0f)
        {
            return 0f;
        }

        float t = (_clock - _shakeStartTime) / _shake.Duration;
        if (t >= 1f)
        {
            return 0f;
        }

        float decay = 1f - t;
        return _shake.Magnitude * decay * decay;
    }

    private Vector3 GetShakeOffset()
    {
        // ProCamera2D의 Shake 확장이 흔들림을 적용할 때는 중복으로 흔들지 않는다.
        if (_proCameraShake != null && _proCameraShake.isActiveAndEnabled)
            return Vector3.zero;

        float strength = GetCurrentShakeStrength();
        if (strength <= 0f)
        {
            return Vector3.zero;
        }

        Vector2 random = Random.insideUnitCircle * strength;
        return new Vector3(random.x, random.y * _verticalShakeRatio, 0f);
    }

    private Vector3 GetKickOffset()
    {
        if (_kick.Distance <= 0f)
        {
            return Vector3.zero;
        }

        float t = (_clock - _kickStartTime) / _kick.Duration;
        if (t >= 1f)
        {
            _kick = default;
            return Vector3.zero;
        }

        // 빠르게 밀렸다가 천천히 돌아옴
        float weight = t < _kick.PushRatio
            ? Mathf.SmoothStep(0f, 1f, t / _kick.PushRatio)
            : 1f - Mathf.SmoothStep(0f, 1f, (t - _kick.PushRatio) / (1f - _kick.PushRatio));

        return (Vector3)(_kickDirection * (_kick.Distance * weight));
    }

    /// <summary>줌 진행도 0~1 (들어감 → 유지 → 돌아옴)</summary>
    private float GetZoomWeight()
    {
        if (!_isZooming)
        {
            return 0f;
        }

        float elapsed = _clock - _zoomStartTime;

        if (elapsed < _zoom.InTime)
        {
            return Mathf.SmoothStep(0f, 1f, elapsed / _zoom.InTime);
        }

        elapsed -= _zoom.InTime;
        if (elapsed < _zoom.HoldTime)
        {
            return 1f;
        }

        elapsed -= _zoom.HoldTime;
        if (elapsed < _zoom.OutTime)
        {
            return 1f - Mathf.SmoothStep(0f, 1f, elapsed / _zoom.OutTime);
        }

        _isZooming = false;
        return 0f;
    }

    private void ApplyZoom(float zoomWeight)
    {
        if (_camera == null || !_camera.orthographic)
        {
            return;
        }

        if (!_isZooming)
        {
            RestoreZoom();

            // 줌 중이 아닐 때는 다른 곳에서 바꾼 크기를 기준으로 삼음
            _baseOrthographicSize = _camera.orthographicSize;
            return;
        }

        SetOrthographicSize(_baseOrthographicSize * (1f - (_zoom.Amount * zoomWeight)));
        _isZoomApplied = true;
    }

    private void SetOrthographicSize(float size)
    {
        if (_proCamera != null && _proCamera.isActiveAndEnabled)
        {
            _proCamera.UpdateScreenSize(size);
        }
        else
        {
            _camera.orthographicSize = size;
        }
    }

    private void RestoreZoom()
    {
        if (!_isZoomApplied || _camera == null)
        {
            return;
        }

        SetOrthographicSize(_baseOrthographicSize);
        _isZoomApplied = false;
    }

    #endregion
}
