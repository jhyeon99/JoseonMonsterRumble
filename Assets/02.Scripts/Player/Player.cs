using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerAttack))]
[RequireComponent(typeof(PlayerSoul))]
public class Player : MonoBehaviour
{
    private StateMachine _stateMachine;
    public StateMachine StateMachine => _stateMachine;

    private PlayerIdleState _idleState;
    public PlayerIdleState IdleState => _idleState;
    private PlayerMoveState _moveState;
    public PlayerMoveState MoveState => _moveState;
    private PlayerJumpState _jumpState;
    public PlayerJumpState JumpState => _jumpState;
    private PlayerAttackState _attackState;
    public PlayerAttackState AttackState => _attackState;
    private PlayerDodgeState _dodgeState;
    public PlayerDodgeState DodgeState => _dodgeState;
    private PlayerDeadState _deadState;
    public PlayerDeadState DeadState => _deadState;

    private PlayerHealth _health;
    public PlayerHealth Health => _health;
    private PlayerMovement _movement;
    public PlayerMovement Movement => _movement;
    private PlayerAttack _attack;
    public PlayerAttack Attack => _attack;
    private PlayerSoul _soul;
    public PlayerSoul Soul => _soul;

    [SerializeField] private Animator _animator;
    public Animator Animator => _animator;
    private static readonly int AnimParamIsRunning = Animator.StringToHash("isRunning");
    private static readonly int AnimParamIsGrounded = Animator.StringToHash("isGrounded");

    private CapsuleCollider2D _capsuleCollider;
    private Transform _feetTransform;
    private readonly Vector2 _groundColliderSize = new Vector2(0.68f, 2.16f);
    private readonly Vector2 _groundColliderOffset = new Vector2(-0.13f, 0.63f);
    private readonly Vector2 _airColliderSize = new Vector2(0.68f, 1.91f);
    private readonly Vector2 _airColliderOffset = new Vector2(-0.13f, 0.755f);
    private readonly Vector3 _groundFeetLocalPos = new Vector3(0f, -0.5f, 0f);
    private readonly Vector3 _airFeetLocalPos = new Vector3(0f, -0.2f, 0f);
    private const float ColliderRestoreDelay = 0.1f;
    private Coroutine _restoreColliderRoutine;

    public bool IsGrounded => _movement != null && _movement.IsGrounded();

    private void OnEnable() => Health.OnDied += HandleDeath;
    private void OnDisable() => Health.OnDied -= HandleDeath;

    private void Awake()
    {
        _health = GetComponent<PlayerHealth>();
        _movement = GetComponent<PlayerMovement>();
        _attack = GetComponent<PlayerAttack>();
        _soul = GetComponent<PlayerSoul>();

        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>();
        }

        if (_capsuleCollider == null)
        {
            _capsuleCollider = GetComponent<CapsuleCollider2D>();
        }

        if (_feetTransform == null)
        {
            Transform feetObj = transform.Find("Feet");
            if (feetObj != null) _feetTransform = feetObj;
        }

        InitializeStates();
    }

    private void Start()
    {
        _stateMachine.Initialize(IdleState);
    }

    private void Update()
    {
        _stateMachine.Update();
        SetGrounded(IsGrounded);
    }

    private void FixedUpdate()
    {
        _stateMachine.FixedUpdate();
        Movement.IntegrateMovement(_stateMachine.CurrentState.PlatformMode);
    }

    private void InitializeStates()
    {
        _stateMachine = new StateMachine();
        _idleState = new PlayerIdleState(this, _stateMachine);
        _moveState = new PlayerMoveState(this, _stateMachine);
        _jumpState = new PlayerJumpState(this, _stateMachine);
        _attackState = new PlayerAttackState(this, _stateMachine);
        _dodgeState = new PlayerDodgeState(this, _stateMachine);
        _deadState = new PlayerDeadState(this, _stateMachine);
    }

    public void SetRunning(bool isRunning)
    {
        if (_animator != null)
        {
            _animator.SetBool(AnimParamIsRunning, isRunning);
        }
    }

    public void SetGrounded(bool isGrounded)
    {
        if (_animator != null)
        {
            _animator.SetBool(AnimParamIsGrounded, isGrounded);
        }
    }

    public void SetAirCollider(bool isAir)
    {
        if (isAir)
        {
            if (_restoreColliderRoutine != null)
            {
                StopCoroutine(_restoreColliderRoutine);
                _restoreColliderRoutine = null;
            }

            if (_capsuleCollider != null)
            {
                _capsuleCollider.size = _airColliderSize;
                _capsuleCollider.offset = _airColliderOffset;
            }

            if (_feetTransform != null)
            {
                _feetTransform.localPosition = _airFeetLocalPos;
            }
        }
        else
        {
            if (_capsuleCollider != null)
            {
                _capsuleCollider.size = _groundColliderSize;
                _capsuleCollider.offset = _groundColliderOffset;
            }

            if (_feetTransform != null)
            {
                _feetTransform.localPosition = _groundFeetLocalPos;
            }
        }
    }

    public void RestoreGroundColliderDelayed()
    {
        if (_restoreColliderRoutine != null)
        {
            StopCoroutine(_restoreColliderRoutine);
        }
        _restoreColliderRoutine = StartCoroutine(RestoreGroundColliderRoutine());
    }

    private IEnumerator RestoreGroundColliderRoutine()
    {
        yield return new WaitForSeconds(ColliderRestoreDelay);
        SetAirCollider(false);
        _restoreColliderRoutine = null;
    }

    private void HandleDeath()
    {
        StateMachine.ChangeState(DeadState);
    }
}