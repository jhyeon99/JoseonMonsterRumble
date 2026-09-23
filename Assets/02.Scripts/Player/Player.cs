using UnityEngine;

[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerAttack))]
public class Player : MonoBehaviour, IDamageable
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

    private void Awake()
    {
        _health = GetComponent<PlayerHealth>();
        _movement = GetComponent<PlayerMovement>();
        _attack = GetComponent<PlayerAttack>();
        
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

    public bool TakeDamage(HitData hitData)
    {
        if (Movement != null && Movement.IsDodging)
        {
            return false;
        }

        return Health.TakeDamage(hitData);
    }

    public void SetInvincible(bool isInvincible)
    {
        _health.SetInvincible(isInvincible);
    }
}