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
    private PlayerAirAttackState _airAttackState;
    public PlayerAirAttackState AirAttackState => _airAttackState;
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
    private PlayerFinisherState _finisherState;
    public PlayerFinisherState FinisherState => _finisherState;
    private PlayerPotionState _potionState;
    public PlayerPotionState PotionState => _potionState;

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
    private PlayerFinisher _finisher;
    public PlayerFinisher Finisher => _finisher;
    private PlayerWeaponBuff _weaponBuff;
    public PlayerWeaponBuff WeaponBuff => _weaponBuff;
    private PlayerPotionAura _potionAura;
    public PlayerPotionAura PotionAura => _potionAura;

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
    private static readonly int AnimParamGuardStart = Animator.StringToHash("guardStart");
    private static readonly int AnimParamParry = Animator.StringToHash("parry");
    private static readonly int AnimParamGuardHit = Animator.StringToHash("guardHit");

    // 마무리 공격: 콤보 전이 없이 3타 모션을 바로 재생
    private const string FinisherAnimState = "Player_Attack_3rd_Ready";
    private static readonly int FinisherAnimStateHash = Animator.StringToHash(FinisherAnimState);
    private static readonly int FinisherAnimStateFullHash = Animator.StringToHash("Base Layer." + FinisherAnimState);

    private const string PotionAnimState = "Player_Heal";
    private static readonly int PotionAnimStateHash = Animator.StringToHash(PotionAnimState);
    private static readonly int PotionAnimStateFullHash = Animator.StringToHash("Base Layer." + PotionAnimState);

    private const string IdleAnimState = "Player_Idle";
    private const string MissingAirAttackWarning = "공중공격: 공중 상태, Animator 연결 및 공중공격 데이터를 확인하세요.";
    private static readonly int IdleAnimStateHash = Animator.StringToHash(IdleAnimState);
    private static readonly int IdleAnimStateFullHash = Animator.StringToHash("Base Layer." + IdleAnimState);

    // 가드/패링 모션이 애니메이터에 추가되기 전에도 경고 없이 동작하도록 파라미터 유무를 저장
    private bool _hasGuardingParam;
    private bool _hasGuardStartParam;
    private bool _hasParryParam;
    private bool _hasGuardHitParam;

    private Coroutine _restoreColliderRoutine;

    public bool IsGrounded => _movement != null && _movement.IsGrounded();

    private void OnEnable()
    {
        Health.OnDied += HandleDeath;
        Health.OnHit += HandleHit;
        Health.OnPotionUsed += HandlePotionUsed;
        Guard.OnParried += HandleParried;
        Guard.OnBlocked += HandleBlocked;
    }

    private void OnDisable()
    {
        Health.OnDied -= HandleDeath;
        Health.OnHit -= HandleHit;
        Health.OnPotionUsed -= HandlePotionUsed;
        Guard.OnParried -= HandleParried;
        Guard.OnBlocked -= HandleBlocked;
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

        _finisher = GetComponent<PlayerFinisher>();
        if (_finisher == null)
        {
            Debug.LogWarning($"{name}: PlayerFinisher가 없어 기본값으로 추가합니다. 수치를 조정하려면 프리팹에 직접 추가하세요.", this);
            _finisher = gameObject.AddComponent<PlayerFinisher>();
        }

        _weaponBuff = GetComponent<PlayerWeaponBuff>();
        if (_weaponBuff == null)
        {
            _weaponBuff = gameObject.AddComponent<PlayerWeaponBuff>();
        }

        _potionAura = GetComponent<PlayerPotionAura>();
        if (_potionAura == null)
        {
            _potionAura = gameObject.AddComponent<PlayerPotionAura>();
        }

        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>();
        }

        _hasGuardingParam = HasAnimatorParameter(AnimParamIsGuarding);
        _hasGuardStartParam = HasAnimatorParameter(AnimParamGuardStart);
        _hasParryParam = HasAnimatorParameter(AnimParamParry);
        _hasGuardHitParam = HasAnimatorParameter(AnimParamGuardHit);

        InitializeStates();
        InitializeMotionData();
        if (!TryGetComponent<PlayerAudio>(out _)) gameObject.AddComponent<PlayerAudio>();
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
        if (PauseController.IsGameplayInputBlocked)
        {
            // 리스폰 로딩 화면이 걷히는 동안에도 착지 애니메이션은 갱신 (공중 포즈로 멈춰 보이지 않게)
            SetGrounded(IsGrounded);
            return;
        }

        _stateMachine.Update();
        HandleRangedAttack();
        SetGrounded(IsGrounded);
    }

    private void FixedUpdate()
    {
        if (PauseController.IsGameplayInputBlocked)
        {
            return;
        }

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
        _airAttackState = new PlayerAirAttackState(this, _stateMachine);
        _dodgeState = new PlayerDodgeState(this, _stateMachine);
        _deadState = new PlayerDeadState(this, _stateMachine);
        _hitState = new PlayerHitState(this, _stateMachine);
        _spellState = new PlayerSpellState(this, _stateMachine);
        _guardState = new PlayerGuardState(this, _stateMachine);
        _finisherState = new PlayerFinisherState(this, _stateMachine);
        _potionState = new PlayerPotionState(this, _stateMachine);
    }

    public bool CanEnterAirAttack()
    {
        return !IsGrounded && _animator != null && _attack != null &&
            (_animator.HasState(0, PlayerAirAttackState.AnimationStateHash) ||
             _animator.HasState(0, PlayerAirAttackState.FullAnimationStateHash)) &&
            _attack.TryGetAirAttackData(out _);
    }

    public bool TryEnterAirAttack()
    {
        if (!CanEnterAirAttack())
        {
            Debug.LogWarning(MissingAirAttackWarning, this);
            return false;
        }
        _stateMachine.ChangeState(_airAttackState);
        return true;
    }

    /// <summary>물약 사용 가능 시 제자리 복용 상태로 전이. 전이했으면 true</summary>
    public bool TryEnterPotion()
    {
        if (!IsGrounded || !_health.CanUsePotion)
        {
            if (!_health.CanUsePotion) GetComponent<PlayerAudio>()?.PlayUnavailable();
            return false;
        }

        _stateMachine.ChangeState(_potionState);
        return true;
    }

    /// <summary>앞에 그로기 적이 있으면 마무리 공격 시작. 시작했으면 true</summary>
    public bool TryEnterFinisher()
    {
        if (!IsGrounded || !_finisher.TryFindTarget(out IFinishable target))
        {
            return false;
        }

        _finisherState.SetTarget(target);
        _stateMachine.ChangeState(_finisherState);
        return true;
    }

    private void HandleRangedAttack()
    {
        if (_rangedAttack == null)
        {
            return;
        }

        IState currentState = _stateMachine.CurrentState;

        if (_inputHandler.TryUseRangedAttackInput() &&
            currentState != DeadState &&
            currentState != SpellState)
        {
            _rangedAttack.PressAttack();
        }

        if (_rangedAttack.IsCharging && _inputHandler.IsRangedAttackKeyUp)
        {
            _rangedAttack.StopCharging();
        }

        if (_rangedAttack.IsReadyToFire && CanReleaseRangedAttack())
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

    public void TriggerPotion()
    {
        if (_animator == null)
        {
            return;
        }

        if (_animator.HasState(0, PotionAnimStateHash) || _animator.HasState(0, PotionAnimStateFullHash))
        {
            _animator.Play(PotionAnimStateHash, 0, 0f);
            return;
        }

        Debug.LogWarning($"{name}: 애니메이터에 {PotionAnimState} 상태가 없습니다.", this);
    }

    public void PrepareForRespawn(Collider2D groundCollider)
    {
        _movement.PrepareForRespawn(groundCollider);
        SetRunning(false);
        SetGrounded(groundCollider != null);
        if (groundCollider != null)
        {
            PlayIdleAnimation();
            if (_animator != null && _animator.isActiveAndEnabled)
            {
                // 시간 정지 중에도 대기 자세를 먼저 적용한 뒤 로딩 화면을 걷는다.
                _animator.Update(0f);
            }
        }
    }

    public void PlayIdleAnimation()
    {
        if (_animator == null)
        {
            return;
        }

        if (_animator.HasState(0, IdleAnimStateHash) || _animator.HasState(0, IdleAnimStateFullHash))
        {
            _animator.Play(IdleAnimStateHash, 0, 0f);
        }
    }

    public void PlayFinisherAnimation()
    {
        if (_animator == null)
        {
            return;
        }

        if (_animator.HasState(0, FinisherAnimStateHash) || _animator.HasState(0, FinisherAnimStateFullHash))
        {
            _animator.Play(FinisherAnimStateHash, 0, 0f);
            return;
        }

        Debug.LogWarning($"{name}: 애니메이터에 {FinisherAnimState} 상태가 없어 attack3 트리거로 대신 재생합니다.", this);
        TriggerAttack3();
    }

    public void SetGuarding(bool isGuarding)
    {
        if (_hasGuardingParam)
        {
            _animator.SetBool(AnimParamIsGuarding, isGuarding);
        }
    }

    public void TriggerGuardStart()
    {
        if (_hasGuardStartParam)
        {
            _animator.SetTrigger(AnimParamGuardStart);
        }
    }

    public void TriggerParry()
    {
        if (_hasParryParam)
        {
            _animator.SetTrigger(AnimParamParry);
        }
    }

    public void TriggerGuardHit()
    {
        if (_hasGuardHitParam)
        {
            _animator.SetTrigger(AnimParamGuardHit);
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

    public bool IsSuperArmorActive()
    {
        if (StateMachine.CurrentState == SpellState && SpellState.IsSuperArmorActive)
        {
            return true;
        }

        if (StateMachine.CurrentState == AttackState && AttackState.IsThirdAttackSuperArmorActive())
        {
            return true;
        }

        return false;
    }

    private void HandleHit(HitData hitData)
    {
        if (StateMachine.CurrentState == DeadState)
        {
            return;
        }

        if (IsSuperArmorActive())
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

    private void HandleBlocked(HitData hitData)
    {
        TriggerGuardHit();
        GuardState.StartBlockKnockback(hitData.AttackDirection);
    }

    private void HandleDeath()
    {
        _rangedAttack?.CancelCharge();
        StateMachine.ChangeState(DeadState);
    }

    private void HandlePotionUsed()
    {
        if (_potionAura != null)
        {
            _potionAura.PlayAura();
        }
    }
}
