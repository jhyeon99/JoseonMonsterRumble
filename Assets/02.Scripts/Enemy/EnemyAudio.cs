using GameAudio;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyAudio : ActorAudio
{
    private Enemy _enemy;
    private EnemyHealth _health;
    private EnemyRangedCombat _ranged;
    private FlyingMeleeAttackController _flyingAttack;
    private ulong _stateExecution;
    private PlayHandle _ambience;
    private PlayHandle _deathSound;
    private bool _dead;
    private Animator _animator;
    private readonly FootstepClock _footsteps = new FootstepClock();
    private string Prefix => _enemy != null && _enemy.MoveType is EnemyFlyingMove ? "Air." : "Ground.";

    protected override void OnEnable()
    {
        base.OnEnable();
        _enemy = GetComponent<Enemy>();
        _health = GetComponent<EnemyHealth>();
        _ranged = GetComponent<EnemyRangedCombat>();
        _flyingAttack = GetComponent<FlyingMeleeAttackController>();
        _dead = false;
        _deathSound = PlayHandle.Empty;
        _animator = GetComponentInChildren<Animator>();
        if (_health != null) { _health.OnHit += OnHit; _health.OnDeath += OnDeath; }
        if (_ranged != null) _ranged.OnProjectileFired += OnProjectileFired;
        if (_flyingAttack != null) _flyingAttack.OnAttackPhaseChanged += OnFlyingAttackPhaseChanged;
        if (_enemy?.StateMachine != null)
        {
            _enemy.StateMachine.OnStateChanged += OnStateChanged;
            if (_enemy.StateMachine.CurrentState != null) OnStateChanged(null, _enemy.StateMachine.CurrentState);
        }
    }

    protected override void OnDisable()
    {
        if (_health != null) { _health.OnHit -= OnHit; _health.OnDeath -= OnDeath; }
        if (_ranged != null) _ranged.OnProjectileFired -= OnProjectileFired;
        if (_flyingAttack != null) _flyingAttack.OnAttackPhaseChanged -= OnFlyingAttackPhaseChanged;
        if (_enemy?.StateMachine != null) _enemy.StateMachine.OnStateChanged -= OnStateChanged;
        base.OnDisable();
        _stateExecution = 0;
        _ambience = PlayHandle.Empty;
    }

    protected override SoundSetSO ResolveSet()
    {
        if (_soundSet != null) return _soundSet;
        AudioCatalogSO catalog = Manager?.Catalog;
        if (catalog == null || _enemy == null) return null;
        if (catalog.TryGetMonsterSet(_enemy.Data, out SoundSetSO monsterSounds)) return monsterSounds;
        if (_enemy is BuffEnemy) return catalog.Buff;
        EnemySoundSets sets = _enemy.MoveType is EnemyFlyingMove ? catalog.Air : catalog.Ground;
        return _ranged != null ? sets?.Ranged : sets?.Melee;
    }

    private void OnStateChanged(EnemyState previous, EnemyState next)
    {
        if (next is LaunchedState)
        {
            AudioCatalogSO catalog = Manager?.Catalog;
            if (catalog != null)
            {
                catalog.GetLaunchDeathFade(_enemy.Data, out float start, out float duration);
                Manager.LimitSfxPlayback(_deathSound, start, duration);
            }
            if (Prefix == "Ground.") PlayResult("Ground.FinisherLaunch", transform.position);
        }
        if (_dead) return;
        bool continuesGroundSwing = previous is AttackPrepareGroundState && next is AttackState &&
            _enemy.Combat is EnemyMeleeCombat && _enemy.MoveType is EnemyGroundMove;
        bool completed = previous is AttackState && next is AttackRecoveryState;
        // 준비에서 시작한 휘두르기 소리와 지연 예약은 타격 판정 상태까지 이어진다.
        if (!continuesGroundSwing)
        {
            EndAction(_stateExecution, completed);
            _stateExecution = 0;
        }
        if (Manager != null) Manager.StopHandle(_ambience);
        _ambience = PlayHandle.Empty;
        if (next is DeadState || next is LaunchedState) return;
        if (_stateExecution == 0) _stateExecution = BeginAction();
        if (next is GroggyState)
        {
            PlayResult(Prefix + "Groggy", transform.position);
        }
        else if (next is AttackState && _ranged == null && !continuesGroundSwing)
        {
            PlayAction(Prefix == "Air." ? "Air.MeleeDash" : "Ground.MeleeAttack", _stateExecution);
        }
        else if (next is AttackPrepareGroundState || next is AttackPrepareFlyingState)
        {
            PlayAction(Prefix + "AttackReady", _stateExecution);
            if (next is AttackPrepareGroundState && _enemy.Combat is EnemyMeleeCombat)
                PlayAction("Ground.MeleeAttack", _stateExecution);
        }
        else if (next is ChaseState && previous is IdleState)
        {
            PlayEntity(Prefix + "Detect");
        }

        if (_enemy.MoveType is EnemyFlyingMove && (next is IdleState || next is ChaseState || next is ReturnToStartState || next is AttackRecoveryState))
        {
            PlayAction("Air.Flight", _stateExecution);
            if (next is IdleState) _ambience = PlayEntity("Air.Ambience");
        }
        if (_enemy is BuffEnemy)
        {
            if (next is BuffState) { PlayAction("Buff.Apply", _stateExecution); PlayAction("Buff.Loop", _stateExecution); }
            else if (next is SelfDestructState)
            {
                PlayAction("Buff.Enrage", _stateExecution);
            }
        }
    }

    private void LateUpdate()
    {
        bool walking = !_dead && _enemy != null && _enemy.MoveType is EnemyGroundMove ground &&
            ground.IsGrounded() && (!_enemy.Data.UseWalkAnimationMotion || !ground.IsWalkStopped) && Mathf.Abs(_enemy.EnemyRb.linearVelocity.x) > 0.05f &&
            !PauseController.IsGameplayInputBlocked && ground.HasWalkFacingControl;
        if (_footsteps.Tick(walking, _animator) && _stateExecution != 0)
            PlayAction("Ground.Footstep", _stateExecution);
    }

    private void OnFlyingAttackPhaseChanged(FlyingMeleeAttackPhase phase)
    {
        if (_dead || !(_enemy?.StateMachine?.CurrentState is FlyingMeleeAttackState)) return;
        if (phase == FlyingMeleeAttackPhase.Preparing)
        {
            PlayAction("Air.AttackReady", _stateExecution);
        }
        else if (phase == FlyingMeleeAttackPhase.Striking)
        {
            PlayAction("Air.MeleeDash", _stateExecution);
        }
        else if (phase == FlyingMeleeAttackPhase.Recovering)
        {
            EndAction(_stateExecution, true);
            _stateExecution = 0;
        }
    }
    private void OnProjectileFired(Vector3 position) => PlayResult(Prefix + "RangedFire", position);
    private void OnHit(HitData hit) { if (!_dead && hit.Damage > 0f) PlayResult(Prefix + "Hurt", hit.HitPoint); }
    private void OnDeath()
    {
        if (_dead) return;
        _dead = true;
        _deathSound = PlayResult(Prefix + "Death", transform.position);
        RetireOwner();
        _stateExecution = 0;
    }
}
