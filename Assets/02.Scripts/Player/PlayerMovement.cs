using UnityEngine;
using System.Collections;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float _speed;
    public float Speed => _speed;
    [SerializeField] private float _jumpForce;

    [SerializeField] private Transform _feetPosition;
    [SerializeField] private Vector2 _groundCheckSize = new Vector2(0.65f, 0.1f);

    [Header("Dodge Settings")]
    [SerializeField] private float _dodgeDistance;

    public float DodgeDistance => _dodgeDistance;

    [SerializeField] private float _dodgeDuration;
    public float DodgeDuration => _dodgeDuration;
    [SerializeField] private float _dodgeCooldown;
    [SerializeField] private AnimationCurve _dodgeSpeedCurve;
    public AnimationCurve DodgeSpeedCurve => _dodgeSpeedCurve;

    [Header("Jump Assist")]
    [SerializeField] private float _coyoteTime = 0.1f;

    [SerializeField] private float _groundCheckLockout = 0.1f;

    private bool _canDodge = true;
    public bool CanDodge => _canDodge;
    private Rigidbody2D _rigidbody2D;

    private MovingPlatform _currentPlatform;
    public MovingPlatform CurrentPlatform => _currentPlatform;

    private bool _hasHorizontalInput;

    private float _coyoteTimer;
    private float _lockoutTimer;
    private bool _isJumping;

    public bool CanJump => !_isJumping && IsGrounded() && _coyoteTimer > 0f;

    private void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (_lockoutTimer > 0f)
        {
            _lockoutTimer -= Time.deltaTime;
            _coyoteTimer = 0f;
            return;
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

    public void SetMovingPlatform(MovingPlatform platform)
    {
        _currentPlatform = platform;
    }

    public bool IsGrounded()
    {
        bool isTouchingGround = Physics2D.OverlapBox(
            _feetPosition.position,
            _groundCheckSize,
            0f,
            Constants.Layers.GroundAllMask
        );

        if (!isTouchingGround) return false;

        if (_currentPlatform != null) return true;

        return _rigidbody2D.linearVelocity.y <= 0.1f;
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
        Vector2 v = _rigidbody2D.linearVelocity;
        v.y = _jumpForce / _rigidbody2D.mass;
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

        if (_rigidbody2D.linearVelocity.y < 0f)
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