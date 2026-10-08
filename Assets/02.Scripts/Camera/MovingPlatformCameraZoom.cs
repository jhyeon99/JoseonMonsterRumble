using Com.LuisPedroFonseca.ProCamera2D;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ProCamera2D))]
public sealed class MovingPlatformCameraZoom : BasePC2D, ISizeDeltaChanger
{
    [Header("이동·낙하 플랫폼 줌아웃")]
    [Tooltip("비워두면 ProCamera2D의 추적 대상에서 플레이어를 자동으로 연결합니다.")]
    [SerializeField] private PlayerMovement _playerMovement;

    [Tooltip("플랫폼에 탔을 때의 카메라 Size입니다. 클수록 넓게 보입니다. Numeric Boundaries의 크기에 따라 제한될 수 있습니다.")]
    [SerializeField, Min(0.01f)] private float _zoomOutSize = 10f;

    [Tooltip("줌 전환의 부드러움입니다. 작을수록 빠르게 전환합니다.")]
    [SerializeField, Min(0.01f)] private float _zoomSmoothness = 0.5f;

    private PlayerHealth _playerHealth;
    private Rigidbody2D _playerBody;
    private readonly ContactPoint2D[] _platformContacts = new ContactPoint2D[32];
    private float _normalSize;
    private float _zoomVelocity;
    private bool _ownsZoom;
    private bool _keepPlatformZoom;

    private enum SupportType
    {
        Air,
        Ground,
        Platform,
        Unavailable
    }

    public int SDCOrder { get; set; } = 1000;

    protected override void Awake()
    {
        base.Awake();
        ProCamera2D.AddSizeDeltaChanger(this);
    }

    private void Start()
    {
        TryBindPlayer();
    }

    public float AdjustSize(float deltaTime, float originalDelta)
    {
        if (!isActiveAndEnabled || deltaTime <= 0f)
            return originalDelta;

        TryBindPlayer();

        SupportType supportType = GetSupportType();
        // 공중에서는 이전 상태를 유지해서 플랫폼 사이 점프 중 줌이 왕복하지 않게 한다.
        if (supportType != SupportType.Air)
            _keepPlatformZoom = supportType == SupportType.Platform;

        bool isRiding = _keepPlatformZoom;
        float currentSize = ProCamera2D.ScreenSizeInWorldCoordinates.y * 0.5f;

        if (isRiding && !_ownsZoom)
        {
            // 탑승 전 크기를 기억하고, 내려오면 같은 크기로 복귀한다.
            _normalSize = currentSize;
            _zoomVelocity = 0f;
            _ownsZoom = true;
        }

        if (!_ownsZoom)
            return originalDelta;

        float targetSize = isRiding ? Mathf.Max(_normalSize, _zoomOutSize) : _normalSize;
        if (Mathf.Abs(currentSize - targetSize) <= 0.001f)
        {
            _zoomVelocity = 0f;
            if (!isRiding)
                _ownsZoom = false;

            return originalDelta + targetSize - currentSize;
        }

        float nextSize = Mathf.SmoothDamp(currentSize, targetSize, ref _zoomVelocity,
            Mathf.Max(0.01f, _zoomSmoothness), Mathf.Infinity, deltaTime);
        return originalDelta + nextSize - currentSize;
    }

    public override void OnReset()
    {
        _keepPlatformZoom = false;
        _ownsZoom = false;
        _zoomVelocity = 0f;
    }

    protected override void OnDisable()
    {
        if (_ownsZoom && ProCamera2D != null && ProCamera2D.isActiveAndEnabled)
            ProCamera2D.UpdateScreenSize(_normalSize);

        OnReset();
        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        if (ProCamera2D != null)
            ProCamera2D.RemoveSizeDeltaChanger(this);

        base.OnDestroy();
    }

    private SupportType GetSupportType()
    {
        if (_playerMovement == null || !_playerMovement.isActiveAndEnabled ||
            _playerBody == null || !_playerBody.simulated ||
            (_playerHealth != null && _playerHealth.IsDead))
        {
            return SupportType.Unavailable;
        }

        bool hasGroundContact = false;
        int contactCount = _playerBody.GetContacts(_platformContacts);
        for (int i = 0; i < contactCount; i++)
        {
            ContactPoint2D contact = _platformContacts[i];
            if (contact.normal.y <= 0.5f)
                continue;

            // 접촉 쌍에서 플레이어 쪽 콜라이더를 제외하고 받쳐주는 지형을 확인한다.
            Collider2D support = contact.collider;
            if (support == null || support.attachedRigidbody == _playerBody)
                support = contact.otherCollider;

            if (support == null || !support.enabled || support.isTrigger ||
                support.attachedRigidbody == _playerBody)
            {
                continue;
            }

            if (support.GetComponentInParent<MovingPlatform>() != null ||
                support.GetComponentInParent<FallingPlatform>() != null)
            {
                return SupportType.Platform;
            }

            if ((Constants.Layers.GroundAllMask & (1 << support.gameObject.layer)) != 0)
                hasGroundContact = true;
        }

        return hasGroundContact ? SupportType.Ground : SupportType.Air;
    }

    private void TryBindPlayer()
    {
        if (_playerMovement == null)
        {
            foreach (CameraTarget target in ProCamera2D.CameraTargets)
            {
                if (target.TargetTransform == null)
                    continue;

                PlayerMovement movement = target.TargetTransform.GetComponentInParent<PlayerMovement>();
                if (movement == null)
                    continue;

                _playerMovement = movement;
                break;
            }
        }

        if (_playerHealth == null && _playerMovement != null)
            _playerHealth = _playerMovement.GetComponent<PlayerHealth>();

        if (_playerBody == null && _playerMovement != null)
            _playerBody = _playerMovement.GetComponent<Rigidbody2D>();
    }
}
