using UnityEngine;
using System.Collections;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float _speed;
    public float Speed => _speed;
    [SerializeField] private float _jumpForce;

    [SerializeField] private Transform _feetPosition;
    [SerializeField] private float _checkRadius;

    [Header("Dodge Settings")]
    [SerializeField] private float _dodgeDistance;
    public float DodgeDistance => _dodgeDistance;

    [SerializeField] private float _dodgeDuration;
    public float DodgeDuration => _dodgeDuration;
    [SerializeField] private float _dodgeCooldown;
    [SerializeField] private AnimationCurve _dodgeSpeedCurve;
    public AnimationCurve DodgeSpeedCurve => _dodgeSpeedCurve;

    private bool _canDodge = true;
    public bool CanDodge => _canDodge;
    private Rigidbody2D _rigidbody2D;

    private MovingPlatform _currentPlatform;
    public MovingPlatform CurrentPlatform => _currentPlatform;

    private bool _hasHorizontalInput;

    private void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
    }

    public void SetMovingPlatform(MovingPlatform platform)
    {
        _currentPlatform = platform;
    }

    public bool IsGrounded()
    {
        bool isTouchingGround = Physics2D.OverlapCircle(
            _feetPosition.position,
            _checkRadius,
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
