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
    private PlayHandle _chargeStepHandle;
    private bool _died, _finisherPlayed;
    public ulong AttackExecutionId => _attackExecution;
    public SoundSetSO SoundSet => ResolveSet();
    private ulong _movementExecution;
    private bool _wasGrounded;
    private bool _groundedInitialized;
    private readonly FootstepClock _footsteps = new FootstepClock();

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
        StopChargeStepSound();
        base.OnDisable();
        _attackExecution = _chargeExecution = 0;
    }

    protected override SoundSetSO ResolveSet() => _soundSet != null ? _soundSet : Manager?.Catalog?.Player;

    private void OnStateChanged(IState previous, IState next)
    {
        bool completed = next == _player.IdleState || next == _player.MoveState;
        EndAction(_movementExecution, completed);
        _movementExecution = 0;
        if (_died || next == _player.DeadState || next == _player.HitState) return;
        _movementExecution = BeginAction();
        if (next == _player.JumpState) PlayAction("Player.Jump", _movementExecution);
        else if (next == _player.DodgeState) PlayAction("Player.Dodge", _movementExecution);
        else if (next == _player.GuardState) PlayAction("Player.GuardEnter", _movementExecution);
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
        if (id != null) PlayAction(id, _attackExecution);
    }

    private void OnAttackStepEnded(bool completed)
    {
        EndAction(_attackExecution, completed);
        _attackExecution = 0;
    }

    private void OnMeleeImpact(HitData hit) => PlayResult("Player.MeleeImpact", hit.HitPoint);
    private void OnHit(HitData hit)
    {
        if (hit.Damage <= 0f || _died) return;
        if (_player == null || !_player.IsSuperArmorActive())
        {
            StopChargeStepSound();
            CancelAllActions();
            _attackExecution = _chargeExecution = 0;
        }
        PlayResult("Player.Hurt", hit.HitPoint);
    }

    private void OnDeath()
    {
        if (_died) return;
        _died = true;
        PlayResult("Player.Death", transform.position);
        StopChargeStepSound();
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
        StopChargeStepSound();
        _chargeStepHandle = PlayAction("Player.ChargeStep", _chargeExecution);
        if (_ranged != null && count == _ranged.SlotCount) PlayAction("Player.ChargeFull", _chargeExecution);
    }

    private void StopChargeStepSound()
    {
        if (Manager != null) Manager.StopHandle(_chargeStepHandle);
        _chargeStepHandle = PlayHandle.Empty;
    }

    private void OnChargeEnded()
    {
        StopChargeStepSound();
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
