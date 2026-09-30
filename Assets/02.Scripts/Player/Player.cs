using System;
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
    private PlayerClimbState _climbState;
    public PlayerClimbState ClimbState => _climbState;

    private PlayerHealth _health;
    public PlayerHealth Health => _health;
    private PlayerMovement _movement;
    public PlayerMovement Movement => _movement;
    private PlayerAttack _attack;
    public PlayerAttack Attack => _attack;
    private PlayerSoul _soul;
    public PlayerSoul Soul => _soul;
    private ClimbableRope _currentRope;
    public ClimbableRope CurrentRope => _currentRope;
    private bool _ropeRegrabBlocked;

    public bool IsGrounded => _movement != null && _movement.IsGrounded();

    private void OnEnable() => Health.OnDied += HandleDeath;
    private void OnDisable() => Health.OnDied -= HandleDeath;

    private void Awake()
    {
        _health = GetComponent<PlayerHealth>();
        _movement = GetComponent<PlayerMovement>();
        _attack = GetComponent<PlayerAttack>();
        _soul = GetComponent<PlayerSoul>();

        InitializeStates();
    }

    private void Start()
    {
        _stateMachine.Initialize(IdleState);
    }

    private void Update()
    {
        _stateMachine.Update();
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
        _climbState = new PlayerClimbState(this, _stateMachine);
    }

    private void HandleDeath()
    {
        StateMachine.ChangeState(DeadState);
    }
    public void EnterRope(ClimbableRope rope)
    {
        if (rope == null)
        {
            return;
        }

        _currentRope = rope;
    }

    public void ExitRope(ClimbableRope rope)
    {
        if (_currentRope != rope)
        {
            return;
        }

        _currentRope = null;
    }

    public bool CanStartClimb(float horizontalInput, float verticalInput)
    {
        if (Mathf.Abs(verticalInput) <= 0.01f)
        {
            _ropeRegrabBlocked = false;
            return false;
        }

        if (_ropeRegrabBlocked || _currentRope == null || Mathf.Abs(horizontalInput) > 0.01f)
        {
            return false;
        }

        float feetY = Movement.FeetPosition.y;
        return (verticalInput > 0.01f && feetY < _currentRope.TopY) ||
               (verticalInput < -0.01f && feetY > _currentRope.BottomY);
    }

    public void BlockRopeRegrabUntilVerticalRelease()
    {
        _ropeRegrabBlocked = true;
    }
}
