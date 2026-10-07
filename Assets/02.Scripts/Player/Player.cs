using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerAttack))]
[RequireComponent(typeof(PlayerSoul))]
[RequireComponent(typeof(PlayerRangedAttack))]
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
    private PlayerSpellState _spellState;
    public PlayerSpellState SpellState => _spellState;
    private PlayerGuardState _guardState;
    public PlayerGuardState GuardState => _guardState;

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
    private PlayerRangedAttack _rangedAttack;
    public PlayerRangedAttack RangedAttack => _rangedAttack;
    private PlayerGuard _guard;
    public PlayerGuard Guard => _guard;

    [SerializeField] private Animator _animator;
    public Animator Animator => _animator;
    private static readonly int AnimParamIsRunning = Animator.StringToHash("isRunning");
    private static readonly int AnimParamIsGrounded = Animator.StringToHash("isGrounded");
    private static readonly int AnimParamHit = Animator.StringToHash("hit");
    private static readonly int AnimParamDie = Animator.StringToHash("die");
    private static readonly int AnimParamDodge = Animator.StringToHash("dodge");
    private static readonly int AnimParamAttack = Animator.StringToHash("attack");
    private static readonly int AnimParamAttack2 = Animator.StringToHash("attack2");
    private static readonly int AnimParamAttack3 = Animator.StringToHash("attack3");
    private static readonly int AnimParamSpell = Animator.StringToHash("spell");
    private static readonly int AnimParamIsGuarding = Animator.StringToHash("isGuarding");
    private static readonly int AnimParamParry = Animator.StringToHash("parry");

    // 가드/패링 모션이 애니메이터에 추가되기 전에도 경고 없이 동작하도록 파라미터 유무를 저장
    private bool _hasGuardingParam;
    private bool _hasParryParam;

    private Coroutine _restoreColliderRoutine;

    public bool IsGrounded => _movement != null && _movement.IsGrounded();

    public bool CanCastSpell => IsGrounded && _rangedAttack != null && _rangedAttack.CanCastSpell;

    private void OnEnable()
    {
        Health.OnDied += HandleDeath;
        Health.OnHit += HandleHit;
        Guard.OnParried += HandleParried;
    }

    private void OnDisable()
    {
        Health.OnDied -= HandleDeath;
        Health.OnHit -= HandleHit;
        Guard.OnParried -= HandleParried;
    }

    private void Awake()
    {
        _inputHandler = GetComponent<PlayerInputHandler>();
        _health = GetComponent<PlayerHealth>();
        _movement = GetComponent<PlayerMovement>();
        _attack = GetComponent<PlayerAttack>();
        _soul = GetComponent<PlayerSoul>();
        _rangedAttack = GetComponent<PlayerRangedAttack>();

        // 프리팹에 PlayerGuard가 없으면 기본값으로 추가 (수치를 조정하려면 프리팹에 직접 추가)
        _guard = GetComponent<PlayerGuard>();
        if (_guard == null)
        {
            Debug.LogWarning($"{name}: PlayerGuard가 없어 기본값으로 추가합니다. 수치를 조정하려면 프리팹에 직접 추가하세요.", this);
            _guard = gameObject.AddComponent<PlayerGuard>();
        }

        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>();
        }

        _hasGuardingParam = HasAnimatorParameter(AnimParamIsGuarding);
        _hasParryParam = HasAnimatorParameter(AnimParamParry);

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
        HandleRangedAttack();
        SetGrounded(IsGrounded);

        if (_inputHandler.IsPotionKeyDown)
        {
            _health.TryUsePotion();
        }
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
        _spellState = new PlayerSpellState(this, _stateMachine);
        _guardState = new PlayerGuardState(this, _stateMachine);
    }

    private void HandleRangedAttack()
    {
        if (_rangedAttack == null || !_rangedAttack.IsCharging)
        {
            return;
        }

        if (!_inputHandler.IsRangedAttackKeyUp)
        {
            return;
        }

        if (!CanReleaseRangedAttack())
        {
            _rangedAttack.CancelCharge();
            return;
        }

        if (_rangedAttack.StopCharging())
        {
            _stateMachine.ChangeState(SpellState);
        }
    }

    private bool CanReleaseRangedAttack()
    {
        IState currentState = _stateMachine.CurrentState;
        return IsGrounded && (currentState == IdleState || currentState == MoveState);
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

    public void TriggerAttack2()
    {
        if (_animator != null)
        {
            _animator.SetTrigger(AnimParamAttack2);
        }
    }

    public void TriggerAttack3()
    {
        if (_animator != null)
        {
            _animator.SetTrigger(AnimParamAttack3);
        }
    }

    public void TriggerSpell()
    {
        if (_animator != null)
        {
            _animator.SetTrigger(AnimParamSpell);
        }
    }

    public void SetGuarding(bool isGuarding)
    {
        if (_hasGuardingParam)
        {
            _animator.SetBool(AnimParamIsGuarding, isGuarding);
        }
    }

    public void TriggerParry()
    {
        if (_hasParryParam)
        {
            _animator.SetTrigger(AnimParamParry);
        }
    }

    private bool HasAnimatorParameter(int nameHash)
    {
        if (_animator == null || _animator.runtimeAnimatorController == null)
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in _animator.parameters)
        {
            if (parameter.nameHash == nameHash)
            {
                return true;
            }
        }

        return false;
    }

    private void HandleHit(HitData hitData)
    {
        if (StateMachine.CurrentState == DeadState)
        {
            return;
        }

        HitState.SetHitData(hitData);
        StateMachine.ChangeState(HitState);
    }

    private void HandleParried(HitData hitData)
    {
        TriggerParry();
    }

    private void HandleDeath()
    {
        _rangedAttack?.CancelCharge();
        StateMachine.ChangeState(DeadState);
    }
}
