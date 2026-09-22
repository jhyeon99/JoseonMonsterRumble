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

    [SerializeField] private float _dodgeJumpForce;
    [SerializeField] private float _dodgeDuration;
    [SerializeField] private float _dodgeCooldown;
    [SerializeField] private AnimationCurve _dodgeSpeedCurve;


    private bool _isDodging;
    private bool _canDodge = true;

    private SpriteRenderer _spriteRenderer;
    private Rigidbody2D _rigidbody2D;
    private PlayerInputHandler _inputHandler;


    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _inputHandler = GetComponent<PlayerInputHandler>();
    }

    private void Update()
    {
        if (_isDodging) return;

        if (IsGrounded())
        {
            if (_inputHandler.IsDodgeKeyDown && _canDodge)
            {
                Dodge();
            }
            else if (_inputHandler.IsJumpKeyDown)
            {
                _rigidbody2D.AddForce(Vector2.up * _jumpForce, ForceMode2D.Impulse);
            }
        }
    }

    private void FixedUpdate()
    {
        if (_isDodging) return;

        MoveX();
    }

    private void MoveX()
    {
        _rigidbody2D.linearVelocity = new Vector2(_inputHandler.MoveInputX * _speed, _rigidbody2D.linearVelocity.y);
        if (Mathf.Abs(_inputHandler.MoveInputX) > 0.01f)
        {
            transform.localScale = new Vector3(Mathf.Sign(_inputHandler.MoveInputX), 1, 1);
        }
    }

    private bool IsGrounded()
    {
        bool isTouchingGround = Physics2D.OverlapCircle(
            _feetPosition.position,
            _checkRadius,
            Constants.Layers.GroundAllMask
        );

        return isTouchingGround && _rigidbody2D.linearVelocity.y <= 0.01f;
    }

    private void Dodge()
    {
        _isDodging = true;
        StartCoroutine(DodgeRoutine());
    }

    private IEnumerator DodgeRoutine()
    {
        _isDodging = true;
        _canDodge = false;
        SetInvincible(true);

        float facingDirection = transform.localScale.x > 0 ? 1f : -1f;
        float backstepDirection = -facingDirection;

        _rigidbody2D.linearVelocity = new Vector2(_rigidbody2D.linearVelocity.x, _dodgeJumpForce);

        float elapsedTime = 0f;

        while (elapsedTime < _dodgeDuration)
        {
            elapsedTime += Time.deltaTime;

            float normalizedTime = Mathf.Clamp01(elapsedTime / _dodgeDuration);

            float currentSpeedFactor = _dodgeSpeedCurve.Evaluate(normalizedTime);

            float currentDodgeSpeed = backstepDirection * _dodgeDistance * currentSpeedFactor;
            _rigidbody2D.linearVelocity = new Vector2(currentDodgeSpeed, _rigidbody2D.linearVelocity.y);

            yield return null;
        }

        SetInvincible(false);

        yield return new WaitUntil(() => IsGrounded());

        _isDodging = false;

        yield return new WaitForSeconds(_dodgeCooldown);

        _canDodge = true;
    }

    private void SetInvincible(bool isInvincible)
    {
        int playerLayer = gameObject.layer;
        int enemyLayer = Constants.Layers.Enemy;

        if (enemyLayer != -1)
        {
            Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, isInvincible);
        }

        if (_spriteRenderer != null)
        {
            Color color = _spriteRenderer.color;
            color.a = isInvincible ? 0.5f : 1.0f;
            _spriteRenderer.color = color;
        }
    }
}