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
    public float DodgeDistance => _dodgeDistance;

    [SerializeField] private float _dodgeJumpForce;
    public float DodgeJumpForce => _dodgeJumpForce;
    [SerializeField] private float _dodgeDuration;
    public float DodgeDuration => _dodgeDuration;
    [SerializeField] private float _dodgeCooldown;
    [SerializeField] private AnimationCurve _dodgeSpeedCurve;
    public  AnimationCurve DodgeSpeedCurve => _dodgeSpeedCurve;

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

    public bool IsGrounded()
    {
        bool isTouchingGround = Physics2D.OverlapCircle(
            _feetPosition.position,
            _checkRadius,
            Constants.Layers.GroundAllMask
        );

        return isTouchingGround && _rigidbody2D.linearVelocity.y <= 0.01f;
    }

    public void MoveX(float x)
    {
        _rigidbody2D.linearVelocity = new Vector2(x * _speed, _rigidbody2D.linearVelocity.y);
        if (Mathf.Abs(x) > 0.01f)
        {
            transform.localScale = new Vector3(Mathf.Sign(x), 1, 1);
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

    public void Jump()
    {
        _rigidbody2D.AddForce(Vector2.up * _jumpForce, ForceMode2D.Impulse);
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
