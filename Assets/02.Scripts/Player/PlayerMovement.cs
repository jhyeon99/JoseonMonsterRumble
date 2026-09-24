using UnityEngine;
using System.Collections;

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
    
    private bool _canDodge = true;

    private Rigidbody2D _rigidbody2D;

    private void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
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
}