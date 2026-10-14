using GameAudio;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerAudio : ActorAudio
{
    private Player _player;
    private PlayerHealth _health;
    private PlayerGuard _guard;
    private PlayerAttack _attack;
    private PlayerRangedAttack _ranged;
    private ulong _attackExecution, _chargeExecution;
    private bool _died, _finisherPlayed;
    public ulong AttackExecutionId => _attackExecution;
    public SoundSetSO SoundSet => ResolveSet();
    private ulong _movementExecution;
    private bool _wasGrounded;
    private bool _groundedInitialized;
    private readonly FootstepClock _footsteps = new FootstepClock();

    // 재생 중인 공격 소리. 회피 캔슬 / 점프 공격 도중 착지로 끊기면 바로 멈춤
    private PlayHandle _attackHandle;
    private PlayHandle _interruptedAttackHandle;

    // 사망 소리. 사망 화면을 넘기면(재시작) 바로 멈춤
    private PlayHandle _deathHandle;

    protected override void OnEnable()
    {
        base.OnEnable();
        _player = GetComponent<Player>();
        _health = GetComponent<PlayerHealth>();
        _guard = GetComponent<PlayerGuard>();
        _attack = GetComponent<PlayerAttack>();
        _ranged = GetComponent<PlayerRangedAttack>();
        _died = _finisherPlayed = false;
        _groundedInitialized = false;
        if (_player?.StateMachine != null) _player.StateMachine.OnStateChanged += OnStateChanged;
        if (_health != null) { _health.OnHit += OnHit; _health.OnDied += OnDeath; _health.OnPotionUsed += OnPotionUsed; }
        if (_guard != null) { _guard.OnBlocked += OnBlocked; _guard.OnParried += OnParried; _guard.OnGuardBroken += OnGuardBroken; }
        if (_attack != null) { _attack.OnAttackStepStarted += OnAttackStepStarted; _attack.OnAttackStepEnded += OnAttackStepEnded; _attack.OnMeleeImpact += OnMeleeImpact; }
        if (_ranged != null) { _ranged.OnFired += OnFired; _ranged.OnChargeStep += OnChargeStep; _ranged.OnChargeEnded += OnChargeEnded; }
        GameEvents.OnFinisherImpact += OnFinisher;
    }

    protected override void OnDisable()
    {
        if (_health != null) { _health.OnHit -= OnHit; _health.OnDied -= OnDeath; _health.OnPotionUsed -= OnPotionUsed; }
        if (_guard != null) { _guard.OnBlocked -= OnBlocked; _guard.OnParried -= OnParried; _guard.OnGuardBroken -= OnGuardBroken; }
        if (_attack != null) { _attack.OnAttackStepStarted -= OnAttackStepStarted; _attack.OnAttackStepEnded -= OnAttackStepEnded; _attack.OnMeleeImpact -= OnMeleeImpact; }
        if (_ranged != null) { _ranged.OnFired -= OnFired; _ranged.OnChargeStep -= OnChargeStep; _ranged.OnChargeEnded -= OnChargeEnded; }
        GameEvents.OnFinisherImpact -= OnFinisher;
        if (_player?.StateMachine != null) _player.StateMachine.OnStateChanged -= OnStateChanged;
        _movementExecution = 0;
        base.OnDisable();
        _attackExecution = _chargeExecution = 0;
    }

    protected override SoundSetSO ResolveSet() => _soundSet != null ? _soundSet : Manager?.Catalog?.Player;

    private void OnStateChanged(IState previous, IState next)
    {
        StopInterruptedAttackSound(previous, next);

        bool completed = next == _player.IdleState || next == _player.MoveState;
        EndAction(_movementExecution, completed);
        _movementExecution = 0;
        if (_died || next == _player.DeadState || next == _player.HitState) return;
        _movementExecution = BeginAction();
        if (next == _player.JumpState) PlayAction("Player.Jump", _movementExecution);
        else if (next == _player.DodgeState) PlayAction("Player.Dodge", _movementExecution);
    }

    private void LateUpdate()
    {
        if (_player == null || _died) return;
        bool grounded = _player.IsGrounded;
        if (PauseController.IsGameplayInputBlocked)
        {
            _wasGrounded = grounded;
            _groundedInitialized = true;
            _footsteps.Tick(false, null);
            return;
        }
        if (_groundedInitialized && !_wasGrounded && grounded)
        {
            PlayResult("Player.Land", transform.position);
        }
        _wasGrounded = grounded;
        _groundedInitialized = true;
        bool walking = grounded && _player.StateMachine.CurrentState == _player.MoveState;
        if (_footsteps.Tick(walking, _player.Animator))
        {
            if (_movementExecution == 0) _movementExecution = BeginAction();
            PlayAction("Player.Footstep", _movementExecution);
        }
    }

    public void PlayUnavailable() => PlayResult("Player.Unavailable", transform.position);

    private void OnAttackStepStarted(int step)
    {
        EndAction(_attackExecution, true);
        _attackExecution = BeginAction();
        if (_player != null && _player.StateMachine.CurrentState == _player.FinisherState) return;
        string id = step == 1 ? "Player.Attack1" : step == 2 ? "Player.Attack2" : step == 3 ? "Player.Attack3" : null;
        _attackHandle = id != null ? PlayAction(id, _attackExecution) : PlayHandle.Empty;
    }

    private void OnAttackStepEnded(bool completed)
    {
        EndAction(_attackExecution, completed);
        _attackExecution = 0;

        // 끝까지 못 가고 끊긴 공격: 어떤 상태로 넘어가는지 보고 소리를 끊을지 정함 (OnStateChanged)
        _interruptedAttackHandle = completed ? PlayHandle.Empty : _attackHandle;
        _attackHandle = PlayHandle.Empty;
    }

    /// <summary>
    /// 끊긴 공격 소리 멈춤: 지상 공격(1 / 2타)을 회피로 캔슬했을 때, 점프 공격 도중 착지 등으로 점프 공격이 끝났을 때.
    /// 그 외(피격 등)로 끊긴 공격 소리는 기존처럼 끝까지 재생
    /// </summary>
    private void StopInterruptedAttackSound(IState previous, IState next)
    {
        if (_interruptedAttackHandle.IsEmpty) return;
        PlayHandle handle = _interruptedAttackHandle;
        _interruptedAttackHandle = PlayHandle.Empty;
        if (_player == null || Manager == null) return;

        bool dodgeCancel = previous == _player.AttackState && next == _player.DodgeState;
        bool airAttackEnded = previous == _player.AirAttackState;
        if (dodgeCancel || airAttackEnded) Manager.StopHandle(handle);
    }

    /// <summary>사망 소리 멈춤 (사망 화면을 넘겨 재시작할 때 StageManager가 호출)</summary>
    public void StopDeathSound()
    {
        if (!_deathHandle.IsEmpty && Manager != null) Manager.StopHandle(_deathHandle);
        _deathHandle = PlayHandle.Empty;
    }

    private void OnMeleeImpact(HitData hit) => PlayResult("Player.MeleeImpact", hit.HitPoint);
    private void OnHit(HitData hit)
    {
        if (hit.Damage <= 0f || _died) return;
        if (_player == null || !_player.IsSuperArmorActive())
        {
            CancelAllActions();
            _attackExecution = _chargeExecution = 0;
        }
        PlayResult("Player.Hurt", hit.HitPoint);
    }

    private void OnDeath()
    {
        if (_died) return;
        _died = true;
        _deathHandle = PlayResult("Player.Death", transform.position);
        RetireOwner();
        _attackExecution = _chargeExecution = 0;
    }

    private void OnBlocked(HitData hit) => PlayResult("Player.Block", hit.HitPoint);
    private void OnParried(HitData hit) => PlayResult("Player.Parry", hit.HitPoint);
    private void OnGuardBroken() => PlayResult("Player.GuardBreak", transform.position);
    private void OnPotionUsed() => PlayResult("Player.PotionUse", transform.position);
    private void OnFired(int count)
    {
        // Result remains a one-shot for the firing group; Entity is owned by each projectile.
        SoundSetSO set = ResolveSet();
        if (count > 0 && set != null && set.TryGetEvent("Player.TalismanFire", out EventBinding binding) &&
            binding.LifetimePolicy == LifetimePolicy.Result)
        {
            PlayResult("Player.TalismanFire", transform.position);
        }
    }

    private void OnChargeStep(int count)
    {
        if (count == 1 || _chargeExecution == 0)
        {
            EndAction(_chargeExecution, false);
            _chargeExecution = BeginAction();
            PlayAction("Player.ChargeLoop", _chargeExecution);
        }
        PlayAction("Player.ChargeStep", _chargeExecution);
        if (_ranged != null && count == _ranged.SlotCount) PlayAction("Player.ChargeFull", _chargeExecution);
    }

    private void OnChargeEnded()
    {
        // 충전은 정상 발사 때도 남은 단계 예약을 취소한다.
        EndAction(_chargeExecution, false);
        _chargeExecution = 0;
    }

    public void BeginFinisher() => _finisherPlayed = false;

    private void OnFinisher(Transform attacker, Transform target)
    {
        if (attacker != transform || _finisherPlayed) return;
        _finisherPlayed = true;
        PlayResult("Player.Finisher", target != null ? target.position : transform.position);
    }
}
