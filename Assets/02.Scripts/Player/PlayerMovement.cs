using UnityEngine;
using System.Collections;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float _speed;
    public float Speed => _speed;
    [SerializeField] private float _jumpHeight;

    [SerializeField] private float _dodgeDistance;

    public float DodgeDistance => _dodgeDistance;

    [SerializeField] private float _dodgeDuration = 0.3f;
    public float DodgeDuration => _dodgeDuration;
    [SerializeField] private float _dodgeInvincibleDuration = 0.3f;
    public float DodgeInvincibleDuration => _dodgeInvincibleDuration;
    [SerializeField] private float _dodgeCooldown = 0f;
    [SerializeField] private AnimationCurve _dodgeSpeedCurve;
    public AnimationCurve DodgeSpeedCurve => _dodgeSpeedCurve;

    [Header("Knockback")]
    [SerializeField] private float _knockbackUpVelocity = 3f;
    public float KnockbackUpVelocity => _knockbackUpVelocity;
    [SerializeField] private float _knockbackDeceleration = 12f;
    public float KnockbackDeceleration => _knockbackDeceleration;
    [SerializeField] private float _knockbackHorizontalMultiplier = 1.8f;
    public float KnockbackHorizontalMultiplier => _knockbackHorizontalMultiplier;

    [SerializeField] private float _coyoteTime = 0.1f;

    [SerializeField] private float _groundCheckLockout = 0.1f;

    [SerializeField] private float _fallGravityMultiplier = 2f;
    [SerializeField] private float _lowJumpGravityMultiplier = 2f;

    [Header("낮은 점프 / 높은 점프 (2단계)")]
    [Tooltip("켜면 점프 키를 Long Jump Hold Time 이상 누르고 있을 때만 높은 점프, 그 전에 떼면 낮은 점프 (슈퍼마리오식 2단계). " +
             "끄면 기존처럼 떼는 순간에 따라 높이가 연속으로 달라짐 (Low Jump Gravity Multiplier)")]
    [SerializeField] private bool _useTwoStepJump = true;

    [Tooltip("높은 점프로 판정되는 최소 누름 시간 (초)")]
    [SerializeField, Min(0f)] private float _longJumpHoldTime = 0.12f;

    [Tooltip("낮은 점프 높이 (Jump Height 대비 비율)")]
    [SerializeField, Range(0.1f, 1f)] private float _shortJumpRatio = 0.5f;
    [SerializeField] private float _maxFallSpeed = 15f;
    [SerializeField, Range(0f, 1f)] private float _addPlatformVelocityMultiplier = 1f;

    [SerializeField] private float _landingJumpDelay = 0.1f;

    [SerializeField] private PhysicsMaterial2D _noFrictionMaterial;
    [SerializeField] private PhysicsMaterial2D _highFrictionMaterial;

    private float _baseGravityScale;

    private bool _canDodge = true;
    public bool CanDodge => _canDodge;
    private Rigidbody2D _rigidbody2D;

    private MovingPlatform _currentPlatform;
    public MovingPlatform CurrentPlatform => _currentPlatform;

    private bool _hasHorizontalInput;

    private float _coyoteTimer;
    private float _lockoutTimer;
    private float _landingJumpTimer;
    private bool _isJumping;

    // 2단계 점프: 이번 점프의 높이가 정해졌는지 / 누른 시간 / 시작 높이
    private bool _jumpHeightDecided = true;
    private float _jumpHoldTimer;
    private float _jumpStartY;

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

        // 넉백 등 외부 힘으로 움직이는 중에는 고마찰을 걸지 않음
        bool idleOnGround = !_isJumping && !_hasHorizontalInput && Mathf.Abs(v.x) < 0.1f && IsGrounded();
        _rigidbody2D.sharedMaterial = idleOnGround ? _highFrictionMaterial : _noFrictionMaterial;

        if (_useTwoStepJump)
        {
            UpdateTwoStepJump(isJumpHeld);
            v = _rigidbody2D.linearVelocity;
        }

        float multiplier = 1f;

        if (v.y < 0f)
            multiplier = _fallGravityMultiplier;
        else if (!_useTwoStepJump && _isJumping && !isJumpHeld)
            multiplier = _lowJumpGravityMultiplier;

        _rigidbody2D.gravityScale = _baseGravityScale * multiplier;

        if (v.y < -_maxFallSpeed)
        {
            v.y = -_maxFallSpeed;
            _rigidbody2D.linearVelocity = v;
        }
    }

    /// <summary>
    /// 2단계 점프 판정: 높이가 정해지기 전에 키를 떼면 낮은 점프 높이까지만 오르도록 상승 속도를 깎음.
    /// Long Jump Hold Time 이상 누르면 높은 점프로 확정 (이후 떼도 끝까지 올라감)
    /// </summary>
    private void UpdateTwoStepJump(bool isJumpHeld)
    {
        if (!_isJumping || _jumpHeightDecided)
        {
            return;
        }

        if (isJumpHeld)
        {
            _jumpHoldTimer += Time.fixedDeltaTime;
            if (_jumpHoldTimer >= _longJumpHoldTime)
            {
                _jumpHeightDecided = true;
            }

            return;
        }

        _jumpHeightDecided = true;

        Vector2 v = _rigidbody2D.linearVelocity;
        if (v.y <= 0f)
        {
            return;
        }

        // 남은 높이만큼만 오르는 속도로 제한 (이미 낮은 점프 높이를 넘었으면 바로 정점)
        float gravity = Mathf.Abs(Physics2D.gravity.y) * _baseGravityScale;
        float shortHeight = _jumpHeight * _shortJumpRatio;
        float remaining = shortHeight - (_rigidbody2D.position.y - _jumpStartY);
        float maxVelocity = remaining > 0f ? Mathf.Sqrt(2f * gravity * remaining) : 0f;

        if (v.y > maxVelocity)
        {
            v.y = maxVelocity;
            _rigidbody2D.linearVelocity = v;
        }
    }

    public void SetMovingPlatform(MovingPlatform platform)
    {
        _currentPlatform = platform;
    }

    public bool IsGrounded()
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
        _jumpHeightDecided = false;
        _jumpHoldTimer = 0f;
        _jumpStartY = _rigidbody2D.position.y;

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