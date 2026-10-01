using UnityEngine;
using System.Collections;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float _speed;
    public float Speed => _speed;
    [SerializeField] private float _jumpHeight;

    [SerializeField] private Transform _feetPosition;

    [Header("Dodge Settings")]
    [SerializeField] private float _dodgeDistance;

    public float DodgeDistance => _dodgeDistance;

    [SerializeField] private float _dodgeDuration = 0.3f;
    public float DodgeDuration => _dodgeDuration;
    [SerializeField] private float _dodgeCooldown = 0f;
    [SerializeField] private AnimationCurve _dodgeSpeedCurve;
    public AnimationCurve DodgeSpeedCurve => _dodgeSpeedCurve;

    [Header("Jump Assist")]
    [SerializeField] private float _coyoteTime = 0.1f;

    [SerializeField] private float _groundCheckLockout = 0.1f;

    [SerializeField] private float _fallGravityMultiplier = 2f;
    [SerializeField] private float _lowJumpGravityMultiplier = 2f;
    [SerializeField] private float _maxFallSpeed = 15f;
    [SerializeField, Range(0f, 1f)] private float _addPlatformVelocityMultiplier = 1f;

    [SerializeField] private float _landingJumpDelay = 0.1f;

    private float _baseGravityScale;

    private bool _canDodge = true;
    public bool CanDodge => _canDodge;
    private Rigidbody2D _rigidbody2D;
    public Vector2 Position => _rigidbody2D.position;
    private Vector2 FeetOffset => _feetPosition != null
        ? (Vector2)_feetPosition.position - (Vector2)transform.position
        : Vector2.zero;
    public Vector2 FeetPosition => _rigidbody2D.position + FeetOffset;

    private MovingPlatform _currentPlatform;
    public MovingPlatform CurrentPlatform => _currentPlatform;

    private bool _hasHorizontalInput;

    private float _coyoteTimer;
    private float _lockoutTimer;
    private float _landingJumpTimer;
    private bool _isJumping;

    public bool CanJump => !_isJumping && _coyoteTimer > 0f && _landingJumpTimer == 0f;

    private readonly ContactPoint2D[] _groundContacts = new ContactPoint2D[8];
    private ContactFilter2D _groundFilter;
    private const float GroundNormalThreshold = 0.7f;
    private const float FootContactTolerance = 0.05f;

    public void OnLanded()
    {
        _isJumping = false;
        _coyoteTimer = _coyoteTime;
        _landingJumpTimer = _landingJumpDelay;
    }

    private void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _baseGravityScale = _rigidbody2D.gravityScale;

        _groundFilter.SetLayerMask(Constants.Layers.GroundAllMask);
        _groundFilter.useTriggers = false;
    }

    private void Update()
    {
        bool grounded = IsGrounded();

        if (_lockoutTimer > 0f)
        {
            _lockoutTimer -= Time.deltaTime;
            _coyoteTimer = 0f;
            return;
        }

        if (_landingJumpTimer > 0f)
        {
            _landingJumpTimer -= Time.deltaTime;
            if (_landingJumpTimer < 0f)
            {
                _landingJumpTimer = 0f;
            }
        }

        if (IsGrounded())
        {
            _coyoteTimer = _coyoteTime;
            _isJumping = false;
        }
        else
        {
            _coyoteTimer -= Time.deltaTime;
        }
    }

    public void ApplyGravity(bool isJumpHeld)
    {
        Vector2 v = _rigidbody2D.linearVelocity;

        float multiplier = 1f;

        if (v.y < 0f)
        {
            multiplier = _fallGravityMultiplier;
        }
        else if (_isJumping && !isJumpHeld)
        {
            multiplier = _lowJumpGravityMultiplier;
        }

        _rigidbody2D.gravityScale = _baseGravityScale * multiplier;

        if (v.y < -_maxFallSpeed)
        {
            v.y = -_maxFallSpeed;
            _rigidbody2D.linearVelocity = v;
        }
    }

    public void SetMovingPlatform(MovingPlatform platform)
    {
        _currentPlatform = platform;
    }

    public bool IsGrounded()
    {
        if (!HasGroundContact()) return false;

        if (_currentPlatform != null) return true;

        return _rigidbody2D.linearVelocity.y <= 0.1f;
    }

    private bool HasGroundContact()
    {
        int count = _rigidbody2D.GetContacts(_groundFilter, _groundContacts);
        for (int i = 0; i < count; i++)
        {
            ContactPoint2D contact = _groundContacts[i];

            if (contact.normal.y <= GroundNormalThreshold) continue;

            // 물리가 실제로 위로 받쳐준 접촉만
            if (contact.normalImpulse <= 0f) continue;

            return true;
        }

        return false;
    }

    public void MoveX(float x)
    {
        _hasHorizontalInput = Mathf.Abs(x) > 0.01f;

        float targetVelocityX = x * _speed;
        _rigidbody2D.linearVelocity = new Vector2(targetVelocityX, _rigidbody2D.linearVelocity.y);

        if (_hasHorizontalInput)
        {
            transform.localScale = new Vector3(Mathf.Sign(x), 1, 1);
        }
    }

    public void Jump()
    {
        _isJumping = true;

        float gravity = Mathf.Abs(Physics2D.gravity.y) * _baseGravityScale;
        float jumpVelocity = Mathf.Sqrt(2f * gravity * _jumpHeight);

        Vector2 v = _rigidbody2D.linearVelocity;

        v.y = jumpVelocity;

        if (_currentPlatform != null)
        {
            v.y += _currentPlatform.Velocity.y * _addPlatformVelocityMultiplier;
        }

        _rigidbody2D.linearVelocity = v;

        _coyoteTimer = 0f;
        _lockoutTimer = _groundCheckLockout;
    }

    public void StartDodgeCooldown()
    {
        StartCoroutine(DodgeCooldownRoutine());
    }

    private IEnumerator DodgeCooldownRoutine()
    {
        _canDodge = false;
        yield return new WaitForSeconds(_dodgeCooldown);
        _canDodge = true;
    }

    public void IntegrateMovement(PlatformInteractionMode mode)
    {
        if (mode != PlatformInteractionMode.Carried) return;

        if (_currentPlatform == null) return;

        if (_rigidbody2D.linearVelocity.y != 0f)
        {
            Vector2 v = _rigidbody2D.linearVelocity;
            v.y = 0f;
            _rigidbody2D.linearVelocity = v;
        }

        Vector2 displacement = _rigidbody2D.linearVelocity * Time.fixedDeltaTime;
        displacement += _currentPlatform.DeltaPosition;

        _rigidbody2D.MovePosition(_rigidbody2D.position + displacement);
    }
}
