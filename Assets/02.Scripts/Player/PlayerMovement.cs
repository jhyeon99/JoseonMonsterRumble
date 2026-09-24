using System.Collections;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float _speed;
    [SerializeField] private float _jumpForce;

    [SerializeField] private Transform _feetPosition;
    [SerializeField] private float _checkRadius;

    [Header("Dodge Settings")]
    [SerializeField] private float _dodgeDistance;
    [SerializeField] private float _dodgeJumpForce;
    [SerializeField] private float _dodgeDuration;
    [SerializeField] private float _dodgeCooldown;
    [SerializeField] private AnimationCurve _dodgeSpeedCurve;

    public bool IsDodging => _isDodging;

    private Rigidbody2D _rigidbody2D;
    private Player _player;
    private PlayerInputHandler _inputHandler;

    private Vector2 _playerVelocity;
    private Vector2 _platformVelocity;

    private bool _isDodging;
    private bool _canDodge = true;
    private bool _isJumpRequested;
    private bool _isDodgeRequested;

    private float _dodgeElapsedTime;
    private float _dodgeDirection;

    private void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _player = GetComponent<Player>();
        _inputHandler = GetComponent<PlayerInputHandler>();
    }

    private void Update()
    {
        if (_isDodging)
            return;

        _isJumpRequested |= _inputHandler.IsJumpKeyDown;
        _isDodgeRequested |= _inputHandler.IsDodgeKeyDown;
    }

    private void FixedUpdate()
    {
        UpdatePlayerVelocity();

        Collider2D groundCollider = GetGroundCollider();
        bool isGrounded = IsGrounded(groundCollider);

        if (_isDodging)
        {
            UpdateDodge(groundCollider, isGrounded);
            ApplyVelocity();
            return;
        }

        if (_isDodgeRequested && _canDodge && isGrounded)
        {
            StartDodge();
        }
        else if (_isJumpRequested && isGrounded)
        {
            Jump();
        }
        else
        {
            UpdatePlatformVelocity(groundCollider, isGrounded);
            MoveX();
        }

        ClearMovementRequests();
        ApplyVelocity();
    }

    private void UpdatePlayerVelocity()
    {
        _playerVelocity =
            _rigidbody2D.linearVelocity - _platformVelocity;
    }

    private void UpdatePlatformVelocity(
        Collider2D groundCollider,
        bool isGrounded
    )
    {
        _platformVelocity = isGrounded
            ? GetPlatformVelocity(groundCollider)
            : Vector2.zero;
    }

    private void MoveX()
    {
        float moveInputX = _inputHandler.MoveInputX;
        _playerVelocity.x = moveInputX * _speed;

        if (Mathf.Abs(moveInputX) <= 0.01f)
            return;

        transform.localScale = new Vector3(
            Mathf.Sign(moveInputX),
            1f,
            1f
        );
    }

    private void Jump()
    {
        _platformVelocity = Vector2.zero;
        _playerVelocity.y += _jumpForce / _rigidbody2D.mass;
    }

    private void StartDodge()
    {
        _isDodging = true;
        _canDodge = false;
        _platformVelocity = Vector2.zero;
        _dodgeElapsedTime = 0f;

        float facingDirection = transform.localScale.x > 0f ? 1f : -1f;
        _dodgeDirection = -facingDirection;

        _playerVelocity.y = _dodgeJumpForce;
        _player.SetInvincible(true);
    }

    private void UpdateDodge(
        Collider2D groundCollider,
        bool isGrounded
    )
    {
        _platformVelocity = Vector2.zero;

        if (_dodgeElapsedTime < _dodgeDuration)
        {
            _dodgeElapsedTime += Time.fixedDeltaTime;

            float normalizedTime = Mathf.Clamp01(
                _dodgeElapsedTime / _dodgeDuration
            );

            float speedFactor =
                _dodgeSpeedCurve.Evaluate(normalizedTime);

            _playerVelocity.x =
                _dodgeDirection * _dodgeDistance * speedFactor;

            return;
        }

        if (!isGrounded)
            return;

        StopDodge();
        UpdatePlatformVelocity(groundCollider, true);
    }

    private void StopDodge()
    {
        _player.SetInvincible(false);
        _isDodging = false;
        StartCoroutine(DodgeCooldownRoutine());
    }

    private IEnumerator DodgeCooldownRoutine()
    {
        yield return new WaitForSeconds(_dodgeCooldown);
        _canDodge = true;
    }

    private Collider2D GetGroundCollider()
    {
        return Physics2D.OverlapCircle(
            _feetPosition.position,
            _checkRadius,
            Constants.Layers.GroundAllMask
        );
    }

    private Vector2 GetPlatformVelocity(Collider2D groundCollider)
    {
        if (groundCollider == null)
            return Vector2.zero;

        MovingPlatform movingPlatform =
            groundCollider.GetComponentInParent<MovingPlatform>();

        return movingPlatform != null
            ? movingPlatform.GetVelocity()
            : Vector2.zero;
    }

    private bool IsGrounded(Collider2D groundCollider)
    {
        return groundCollider != null &&
               _playerVelocity.y <= 0.01f;
    }

    private void ClearMovementRequests()
    {
        _isJumpRequested = false;
        _isDodgeRequested = false;
    }

    private void ApplyVelocity()
    {
        _rigidbody2D.linearVelocity =
            _playerVelocity + _platformVelocity;
    }
}
