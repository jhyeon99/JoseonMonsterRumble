using UnityEngine;

public sealed partial class Ep1TutorialManager
{
    [SerializeField] private bool _requirePracticeInput = true;
    private Enemy _guardPracticeEnemy;
    private bool _guardPracticeAttackStarted;
    private bool _highJumpPracticeActive;
    private GroggyState _heldPracticeGroggy;
    private Ep1HighJumpHint _finisherPracticeHint;

    private sealed class GuardPracticeWaitingState : EnemyState
    {
        private readonly Enemy _enemy;

        public GuardPracticeWaitingState(Enemy enemy) => _enemy = enemy;

        public override void Enter()
        {
            _enemy.Combat.StopAttack();
            _enemy.MoveType.ResetLightKnockback();
            _enemy.MoveType.StopMove();
            _enemy.Animation.SetRunning(false);
            _enemy.Animation.ResetAttackTrigger();
            _enemy.DirectionLock?.UnlockDirection();
        }

        public override void FixedUpdate() => _enemy.MoveType.StopMove();
    }

    private bool IsHighJumpPracticePending => _requirePracticeInput && _highJumpHintPoint != null &&
        !HasCompletedHint(Ep1TutorialHintType.HighJump);

    private bool IsFinisherPractice => _requirePracticeInput && _combatStep == CombatStep.WaitingForFinisher &&
        _parryFinisherExplained && !_secondFinisherSucceeded;

    private void UpdateRequiredPractice(bool advanceHold = false)
    {
        if (!_requirePracticeInput)
        {
            ReleaseRequiredPractice();
            return;
        }
        if (_combatPlayer == null || _combatInput == null || _guard == null) return;
        UpdatePracticeTargetProtection();
        _guard.SetTutorialGuardPractice(this, IsGuardDemonstrationActive ||
            (_combatStep == CombatStep.WaitingForRegularGuard && !_regularGuardSucceeded &&
            !IsCheckpointTutorialPending && !IsHighJumpPracticePending));
        if (!_regularGuardSucceeded && !_guardPracticeAttackStarted) ParkGuardPracticeEnemy();
        GameplayInputActions actions = GameplayInputActions.All;
        bool parryPractice = false;
        if (IsGuardDemonstrationActive)
        {
            actions = GameplayInputActions.None;
        }
        else if (_highJumpPracticeActive && IsHighJumpPracticePending)
        {
            actions = GameplayInputActions.Movement | GameplayInputActions.Jump;
        }
        else if (_combatStep == CombatStep.WaitingForRegularGuard)
        {
            if (advanceHold && !PauseController.IsGameplayInputBlocked && !SceneArrivalFade.IsTransitioning &&
                !IsCheckpointTutorialPending && !IsHighJumpPracticePending)
            {
                UpdateGuardPractice();
            }
            actions = IsGuardDemonstrationActive ? GameplayInputActions.None :
                GameplayInputActions.Movement | GameplayInputActions.Guard;
        }
        else if ((_combatStep == CombatStep.WaitingForParryHint ||
            _combatStep == CombatStep.WaitingForFinisher) && !_secondFinisherSucceeded)
        {
            parryPractice = !_parryFinisherExplained;
            actions = GameplayInputActions.Movement |
                (parryPractice ? GameplayInputActions.Guard : GameplayInputActions.Attack);
            if (IsFinisherPractice) EnsureFinisherPractice();
        }
        else if (IsTalismanTapPractice || IsTalismanChargePractice)
        {
            actions = GameplayInputActions.Movement | GameplayInputActions.RangedAttack;
        }
        else if (_talismanStep == TalismanStep.ConfirmingTap || _talismanStep == TalismanStep.ConfirmingCharge)
        {
            actions = GameplayInputActions.Movement;
        }

        if (actions == GameplayInputActions.All) _combatInput.ClearTutorialInputRestriction(this);
        else _combatInput.SetTutorialInputRestriction(this, actions);
    }

    private void UpdateGuardPractice()
    {
        if (_regularGuardSucceeded || IsGuardDemonstrationActive || !_combatInput.IsGuardKey ||
            !_combatPlayer.IsGrounded || _guardPracticeEnemy == null || _guardPracticeEnemy.IsDefeated)
        {
            return;
        }
        BeginGuardDemonstration();
        _guardPracticeAttackStarted = true;
        if (_guardPracticeEnemy != null && !_guardPracticeEnemy.IsDefeated)
        {
            float facing = Mathf.Sign(_guardPracticeEnemy.transform.position.x - _combatPlayer.transform.position.x);
            if (facing != 0f)
            {
                Vector3 scale = _combatPlayer.transform.localScale;
                scale.x = Mathf.Abs(scale.x) * facing;
                _combatPlayer.transform.localScale = scale;
            }
            // Chase closes the distance before the enemy plays its native windup and hitbox attack.
            _guardPracticeEnemy.StateMachine.ChangeState(new ChaseState(_guardPracticeEnemy));
        }
    }

    private void ParkGuardPracticeEnemy()
    {
        if (_guardPracticeEnemy == null && _boundPrerequisites != null && _boundPrerequisites.Length > 0 &&
            _boundPrerequisites[0] != null)
        {
            _guardPracticeEnemy = _boundPrerequisites[0].GetComponent<Enemy>();
        }
        if (_guardPracticeEnemy == null || !_guardPracticeEnemy.isActiveAndEnabled ||
            _guardPracticeEnemy.IsDefeated || _guardPracticeEnemy.StateMachine.CurrentState is GuardPracticeWaitingState)
        {
            return;
        }
        _guardPracticeEnemy.StateMachine.ChangeState(new GuardPracticeWaitingState(_guardPracticeEnemy));
    }

    private void UpdatePracticeTargetProtection()
    {
        if (_boundPrerequisites != null)
        {
            for (int i = 0; i < _boundPrerequisites.Length; i++)
            {
                EnemyHealth target = _boundPrerequisites[i];
                if (target == null) continue;
                bool protect = i == 0 ? !_regularGuardSucceeded || !_guardGaugeExplained : !_secondFinisherSucceeded;
                target.SetTutorialProtection(this, protect, i == 1 && IsFinisherPractice);
                if (i == 0 && target.TryGetComponent(out EnemyMeleeCombat melee))
                    melee.SetTutorialParrySuppressed(this, target.IsAlive);
            }
        }
        if (_talismanTarget != null) _talismanTarget.SetTutorialProtection(this, !_talismanExplained);
    }

    private void EnsureFinisherPractice()
    {
        if (_hasCombatEnemySequence && IsSecondEnemyEncountered())
        {
            Enemy enemy = _boundPrerequisites[1].GetComponent<Enemy>();
            // After a respawn, resume the unfinished finisher without repeating a completed parry lesson.
            if (!enemy.IsGroggy && !enemy.IsDefeated) enemy.EnterGroggy(2f);
            var groggy = enemy.StateMachine.CurrentState as GroggyState;
            if (groggy != null)
            {
                _heldPracticeGroggy = groggy;
                groggy.Hold();
            }
        }
        if (_finisherPracticeHint != null) return;
        GameObject prefab = Resources.Load<GameObject>("Ep1RespawnHint");
        if (prefab == null) return;
        _finisherPracticeHint = Instantiate(prefab, transform, false).GetComponent<Ep1HighJumpHint>();
        _finisherPracticeHint.Configure(this, Ep1TutorialHintType.Finisher);
    }

    private void ReleaseRequiredPractice()
    {
        ReleaseGuardDemonstration();
        _guardPracticeAttackStarted = false;
        if (_guardPracticeEnemy != null && !_guardPracticeEnemy.IsDefeated &&
            _guardPracticeEnemy.StateMachine.CurrentState is GuardPracticeWaitingState)
        {
            _guardPracticeEnemy.StateMachine.ChangeState(new ChaseState(_guardPracticeEnemy));
        }
        _guardPracticeEnemy = null;
        _highJumpPracticeActive = false;
        if (_combatInput != null) _combatInput.ClearTutorialInputRestriction(this);
        if (_guard != null) _guard.SetTutorialGuardPractice(this, false);
        if (_combatPlayer == null || _combatPlayer.StateMachine.CurrentState != _combatPlayer.FinisherState)
            _heldPracticeGroggy?.Release();
        _heldPracticeGroggy = null;
        if (_finisherPracticeHint != null) Destroy(_finisherPracticeHint.gameObject);
        _finisherPracticeHint = null;
        if (_boundPrerequisites != null)
        {
            foreach (EnemyHealth target in _boundPrerequisites)
                if (target != null) target.SetTutorialProtection(this, false);
            if (_boundPrerequisites.Length > 0 && _boundPrerequisites[0] != null &&
                _boundPrerequisites[0].TryGetComponent(out EnemyMeleeCombat melee))
            {
                melee.SetTutorialParrySuppressed(this, false);
            }
        }
        if (_talismanTarget != null) _talismanTarget.SetTutorialProtection(this, false);
    }
}
