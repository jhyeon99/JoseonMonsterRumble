using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(20)]
public class FinisherScreenImpact : MonoBehaviour
{
    private enum PulseState
    {
        Idle,
        Preparing,
        Impact,
        Recovering,
    }

    // Capture ends at Foreground. The effect must render after that capture.
    private const string OverlayLayer = "UI";
    private const string OverlayName = "FinisherGrungeMask";
    private const int OverlayOrder = 32000;
    private const float OverlayDepth = 1f;
    private const int SourceCapacity = 16;
    private const float ImpactCurveProgress = 0.52f;
    private const float MinimumRadius = 0.22f;
    private const float MaximumRadius = 0.55f;
    private const float SubjectRadiusPadding = 0.8f;
    private static readonly int CenterAspectId = Shader.PropertyToID("_CenterAspect");
    private static readonly int BurstId = Shader.PropertyToID("_Burst");
    private static readonly int ScreenFadeId = Shader.PropertyToID("_ScreenFade");
    private static readonly int RestorationProgressId = Shader.PropertyToID("_RestorationProgress");
    private static readonly int SubjectPassId = Shader.PropertyToID("_SubjectPass");
    private static readonly int SubjectColorId = Shader.PropertyToID("_SubjectColor");

    [Tooltip("히트스탑 종료 후 내부에서 원래 색이 퍼져 나오는 시간. 실제 초 기준")]
    [SerializeField, Min(0.01f)] private float _duration = 0.25f;
    [SerializeField] private Material _shockwaveMaterial;
    [Tooltip("캐릭터 크기로 계산한 먹 마스크 반지름의 배율")]
    [SerializeField, Min(0.01f)] private float _shockwaveSize = 1f;
    [Tooltip("가로축은 전체 시간의 비율, 세로축은 크기. 살짝 응축한 뒤 빠르게 퍼집니다.")]
    [SerializeField]
    private AnimationCurve _expansion = new AnimationCurve(
        new Keyframe(0f, 0.08f), new Keyframe(0.12f, 0.055f),
        new Keyframe(0.28f, 0.25f), new Keyframe(0.36f, 1f), new Keyframe(1f, 1f));
    [Tooltip("먹 내부가 열리는 비율. 0은 채워진 먹, 1은 붓 질감의 가장자리만 남은 상태")]
    [SerializeField]
    private AnimationCurve _breakup = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.28f, 0f),
        new Keyframe(0.52f, 1f), new Keyframe(1f, 1f));
    [Tooltip("타격 전 붓 마스크 안의 흑백 강도. 타격 후에는 강도를 유지하며 내부부터 원래 색이 드러납니다.")]
    [SerializeField]
    private AnimationCurve _screenFade = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.18f, 0.1f),
        new Keyframe(0.48f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0f));
    [Tooltip("먹의 불투명도. 중앙이 열린 뒤 가장자리의 먹도 사라집니다.")]
    [SerializeField]
    private AnimationCurve _inkFade = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.1f, 0.18f),
        new Keyframe(0.5f, 0.12f), new Keyframe(1f, 0f));

    private readonly List<SpriteRenderer> _sources = new List<SpriteRenderer>(SourceCapacity);
    private Camera _camera;
    private SpriteRenderer _shockwave;
    private SpriteRenderer _subjectOverlay;
    private SpriteRenderer _subjectSource;
    private Sprite _overlaySprite;
    private MaterialPropertyBlock _shockwaveProperties;
    private Vector3 _impactPosition;
    private PulseState _state;
    private float _elapsed;
    private float _anticipationProgress;
    private float _subjectRadius;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        if (!_camera.orthographic || _shockwaveMaterial == null)
        {
            Debug.LogWarning("[FinisherScreenImpact] 직교 카메라와 먹 마스크 재질이 필요합니다.", this);
            enabled = false;
            return;
        }

        _overlaySprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
        var overlayObject = new GameObject(OverlayName);
        overlayObject.layer = gameObject.layer;
        overlayObject.transform.SetParent(transform, false);
        _shockwave = overlayObject.AddComponent<SpriteRenderer>();
        _shockwave.sprite = _overlaySprite;
        _shockwave.sharedMaterial = _shockwaveMaterial;
        _shockwave.sortingLayerID = SortingLayer.NameToID(OverlayLayer);
        _shockwave.sortingOrder = OverlayOrder;
        _shockwave.enabled = false;
        _shockwaveProperties = new MaterialPropertyBlock();
        var subjectObject = new GameObject("FinisherSubject");
        subjectObject.layer = gameObject.layer;
        _subjectOverlay = subjectObject.AddComponent<SpriteRenderer>();
        _subjectOverlay.sharedMaterial = _shockwaveMaterial;
        _subjectOverlay.sortingLayerID = _shockwave.sortingLayerID;
        _subjectOverlay.sortingOrder = OverlayOrder + 1;
        _subjectOverlay.enabled = false;
    }

    private void OnEnable()
    {
        GameEvents.OnFinisherImpact += HandleFinisher;
        GameEvents.OnFinisherAnticipation += HandleAnticipation;
        GameEvents.OnFinisherAnticipationCancelled += HandleAnticipationCancelled;
    }

    private void OnDisable()
    {
        GameEvents.OnFinisherImpact -= HandleFinisher;
        GameEvents.OnFinisherAnticipation -= HandleAnticipation;
        GameEvents.OnFinisherAnticipationCancelled -= HandleAnticipationCancelled;
        StopPulse();
    }

    private void OnDestroy()
    {
        if (_shockwave != null)
        {
            Destroy(_shockwave.gameObject);
        }
        if (_overlaySprite != null)
        {
            Destroy(_overlaySprite);
        }
        if (_subjectOverlay != null)
        {
            Destroy(_subjectOverlay.gameObject);
        }
    }

    private void HandleFinisher(Transform attacker, Transform target)
    {
        if (_shockwave == null || PauseController.IsPaused || (attacker == null && target == null))
        {
            return;
        }

        // Keep the preparation origin. ReceiveFinisher can already have moved the target.
        if (_state != PulseState.Preparing)
        {
            InitializeOrigin(attacker, target);
        }
        _anticipationProgress = 1f;
        _elapsed = 0f;
        _state = PulseState.Impact;
        UpdateVisuals();
    }

    private void HandleAnticipation(Transform attacker, Transform target, float progress)
    {
        if (_shockwave == null || PauseController.IsPaused || attacker == null || target == null)
        {
            return;
        }
        if (_state != PulseState.Preparing)
        {
            StopPulse();
            InitializeOrigin(attacker, target);
            _state = PulseState.Preparing;
        }
        _anticipationProgress = Mathf.Clamp01(progress);
        UpdateVisuals();
    }

    private void HandleAnticipationCancelled()
    {
        if (_state == PulseState.Preparing)
        {
            StopPulse();
        }
    }

    private void InitializeOrigin(Transform attacker, Transform target)
    {
        _subjectSource = GetBodyRenderer(target != null ? target : attacker);
        Bounds targetBounds = _subjectSource != null ? _subjectSource.bounds : new Bounds((target != null ? target : attacker).position, Vector3.one);
        if (target != null)
        {
            EnemyHurtbox hurtbox = target.GetComponentInChildren<EnemyHurtbox>();
            if (hurtbox != null && hurtbox.TryGetComponent(out Collider2D bodyCollider))
            {
                targetBounds = bodyCollider.bounds;
            }
        }
        SpriteRenderer attackerRenderer = attacker != null ? GetBodyRenderer(attacker) : null;
        Vector3 attackerCenter = attackerRenderer != null ? attackerRenderer.bounds.center : targetBounds.center;
        // Use the body surface facing the attacker, not the monster's root at its feet.
        _impactPosition = targetBounds.ClosestPoint(attackerCenter);
        _impactPosition.y = targetBounds.center.y;
        Bounds subjects = targetBounds;
        if (attackerRenderer != null)
        {
            subjects.Encapsulate(attackerRenderer.bounds);
        }
        _subjectRadius = Mathf.Clamp(subjects.size.magnitude * SubjectRadiusPadding / (_camera.orthographicSize * 2f), MinimumRadius, MaximumRadius);
    }

    private SpriteRenderer GetBodyRenderer(Transform root)
    {
        SpriteRenderer body = null;
        float largestArea = 0f;
        _sources.Clear();
        root.GetComponentsInChildren(false, _sources);
        foreach (SpriteRenderer source in _sources)
        {
            if (source.enabled && source.sprite != null)
            {
                float area = source.bounds.size.x * source.bounds.size.y;
                if (area > largestArea)
                {
                    largestArea = area;
                    body = source;
                }
            }
        }
        _sources.Clear();
        return body;
    }

    private void LateUpdate()
    {
        if (_state == PulseState.Idle)
        {
            return;
        }

        if (PauseController.IsPaused || _state == PulseState.Preparing)
        {
            UpdateVisuals();
            return;
        }
        if (_state == PulseState.Impact)
        {
            if (HitStop.IsActive)
            {
                UpdateVisuals();
                return;
            }
            _state = PulseState.Recovering;
            _elapsed = 0f;
        }
        _elapsed += Time.unscaledDeltaTime;
        if (_elapsed >= _duration)
        {
            StopPulse();
            return;
        }

        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        float progress = _state == PulseState.Preparing
            ? _anticipationProgress * ImpactCurveProgress
            : ImpactCurveProgress;
        float restorationProgress = _state == PulseState.Recovering ? Mathf.Clamp01(_elapsed / _duration) : 0f;
        float height = _camera.orthographicSize * 2f;
        _shockwave.transform.localPosition = Vector3.forward * (_camera.nearClipPlane + OverlayDepth);
        _shockwave.transform.localScale = new Vector3(height * _camera.aspect, height, 1f);
        Vector3 center = _camera.WorldToViewportPoint(_impactPosition);
        float outerRadius = Mathf.Max(0f, _expansion.Evaluate(progress)) * _subjectRadius * _shockwaveSize;
        _shockwaveProperties.SetVector(CenterAspectId, new Vector4(center.x, center.y, _camera.aspect, 0f));
        _shockwaveProperties.SetVector(BurstId, new Vector4(outerRadius, Mathf.Clamp01(_breakup.Evaluate(progress)), Mathf.Clamp01(_inkFade.Evaluate(progress)), progress));
        _shockwaveProperties.SetFloat(ScreenFadeId, Mathf.Clamp01(_screenFade.Evaluate(progress)));
        _shockwaveProperties.SetFloat(RestorationProgressId, restorationProgress);
        _shockwaveProperties.SetFloat(SubjectPassId, 0f);
        _shockwave.SetPropertyBlock(_shockwaveProperties);
        _shockwave.enabled = true;
        if (_subjectSource != null && _subjectSource.enabled && _subjectSource.gameObject.activeInHierarchy)
        {
            _subjectOverlay.sprite = _subjectSource.sprite;
            _subjectOverlay.flipX = _subjectSource.flipX;
            _subjectOverlay.flipY = _subjectSource.flipY;
            _subjectOverlay.transform.SetPositionAndRotation(_subjectSource.transform.position, _subjectSource.transform.rotation);
            _subjectOverlay.transform.localScale = _subjectSource.transform.lossyScale;
            _shockwaveProperties.SetFloat(SubjectPassId, 1f);
            _shockwaveProperties.SetVector(SubjectColorId, _subjectSource.color);
            _subjectOverlay.SetPropertyBlock(_shockwaveProperties);
            _subjectOverlay.enabled = true;
        }
        else
        {
            _subjectOverlay.enabled = false;
        }
    }

    private void StopPulse()
    {
        if (_shockwave != null)
        {
            _shockwave.enabled = false;
        }
        if (_subjectOverlay != null)
        {
            _subjectOverlay.enabled = false;
        }
        _subjectSource = null;
        _subjectRadius = 0f;
        _sources.Clear();
        _elapsed = 0f;
        _anticipationProgress = 0f;
        _impactPosition = Vector3.zero;
        _state = PulseState.Idle;
    }
}
