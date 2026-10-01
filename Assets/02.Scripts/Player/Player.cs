using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

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
    private PlayerHitState _hitState;
    public PlayerHitState HitState => _hitState;

    [Header("Motion Data")]
    [SerializeField] private ActionMotionData _hitMotion = new ActionMotionData();

    public ActionMotionData HitMotion => _hitMotion;
    [SerializeField] private ActionMotionData _dodgeMotion = new ActionMotionData();
    public ActionMotionData DodgeMotion => _dodgeMotion;
    [SerializeField] private ActionMotionData _attackMotion = new ActionMotionData();
    public ActionMotionData AttackMotion => _attackMotion;

    private PlayerInputHandler _inputHandler;
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
    private static readonly int AnimParamHit = Animator.StringToHash("hit");
    private static readonly int AnimParamDie = Animator.StringToHash("die");
    private static readonly int AnimParamDodge = Animator.StringToHash("dodge");
    private static readonly int AnimParamAttack = Animator.StringToHash("attack");

    private Vector2 _baseColliderOffset;
    private Coroutine _restoreColliderRoutine;

    public bool IsGrounded => _movement != null && _movement.IsGrounded();

    private void OnEnable()
    {
        Health.OnDied += HandleDeath;
        Health.OnHit += HandleHit;
    }

    private void OnDisable()
    {
        Health.OnDied -= HandleDeath;
        Health.OnHit -= HandleHit;
    }

    private void Awake()
    {
        _inputHandler = GetComponent<PlayerInputHandler>();
        _health = GetComponent<PlayerHealth>();
        _movement = GetComponent<PlayerMovement>();
        _attack = GetComponent<PlayerAttack>();
        _soul = GetComponent<PlayerSoul>();

        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>();
        }

        InitializeStates();
        InitializeMotionData();
    }

    private void InitializeMotionData()
    {
        if (_animator != null && _animator.runtimeAnimatorController != null)
        {
            AnimationClip[] clips = _animator.runtimeAnimatorController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i].name == "Player_Hit")
                {
                    _hitMotion.BindClipIfEmpty(clips[i], 1.5f);
                }
                else if (clips[i].name == "Player_dodge")
                {
                    _dodgeMotion.BindClipIfEmpty(clips[i], 1.5f);
                }
                else if (clips[i].name == "Player_Attack")
                {
                    _attackMotion.BindClipIfEmpty(clips[i], 1.0f);
                }
            }
        }
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
        Movement.ApplyGravity(_inputHandler.IsJumpKey);
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
        _hitState = new PlayerHitState(this, _stateMachine);
    }

    public void SetRunning(bool isRunning)
    {
        if (_animator != null)
        {
            _animator.SetBool(AnimParamIsRunning, isRunning);
        }
    }

    private void SetGrounded(bool isGrounded)
    {
        if (_animator != null)
        {
            _animator.SetBool(AnimParamIsGrounded, isGrounded);
        }
    }

    public void TriggerHit()
    {
        if (_animator != null)
        {
            _animator.SetTrigger(AnimParamHit);
        }
    }

    public void TriggerDie()
    {
        if (_animator != null)
        {
            _animator.SetTrigger(AnimParamDie);
        }
    }

    public void TriggerDodge()
    {
        if (_animator != null)
        {
            _animator.SetTrigger(AnimParamDodge);
        }
    }

    public void TriggerAttack()
    {
        if (_animator != null)
        {
            _animator.SetTrigger(AnimParamAttack);
        }
    }

    private void HandleHit()
    {
        if (StateMachine.CurrentState == DeadState)
        {
            return;
        }

        StateMachine.ChangeState(HitState);
    }

    private void HandleDeath()
    {
        StateMachine.ChangeState(DeadState);
    }
}