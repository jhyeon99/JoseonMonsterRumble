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

    protected override void OnEnable()
    {
        base.OnEnable();
        _player = GetComponent<Player>();
        _health = GetComponent<PlayerHealth>();
        _guard = GetComponent<PlayerGuard>();
        _attack = GetComponent<PlayerAttack>();
        _ranged = GetComponent<PlayerRangedAttack>();
        _died = _finisherPlayed = false;
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
        base.OnDisable();
        _attackExecution = _chargeExecution = 0;
    }

    protected override SoundSetSO ResolveSet() => _soundSet != null ? _soundSet : Manager?.Catalog?.Player;

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
        RetireOwner();
        _attackExecution = _chargeExecution = 0;
    }

    private void OnBlocked(HitData hit) => PlayResult("Player.Block", hit.HitPoint);
    private void OnParried(HitData hit) => PlayResult("Player.Parry", hit.HitPoint);
    private void OnGuardBroken() => PlayResult("Player.GuardBreak", transform.position);
    private void OnPotionUsed() => PlayResult("Player.PotionUse", transform.position);
    private void OnFired(int count) { if (count > 0) PlayResult("Player.TalismanFire", transform.position); }

    private void OnChargeStep(int count)
    {
        if (count == 1 || _chargeExecution == 0)
        {
            EndAction(_chargeExecution, false);
            _chargeExecution = BeginAction();
            PlayAction("Player.ChargeLoop", _chargeExecution);
        }
        PlayAction("Player.ChargeStep", _chargeExecution);
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
