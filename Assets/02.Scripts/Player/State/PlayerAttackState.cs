using UnityEngine;

public class PlayerAttackState : PlayerBaseState
{
    private const float DefaultComboStartNormalizedTime = 0.7f;
    private const float DefaultComboEndNormalizedTime = 0.5f;
    private const float LedgeSafetyMargin = 0.1f;
    private const float LedgeRayDistance = 0.6f;
    private const float LedgeRayOriginYOffset = 0.1f;
    private const int LedgeCheckSteps = 5;
    private const float AttackSafetyTimeout = 5.0f;
    private const float DodgeCancelNormalizedTime = 0.2f;

    private readonly float[] _cachedStepDurations = new float[3];
    private bool _hasCachedStepDurations;

    private float _elapsedTime;
    private float _comboElapsedTime;
    private int _currentComboIndex;
    private bool _hasEnteredAttackAnimation;

    private bool _hasForwardMotion;
    private AnimationCurve _forwardDisplacementCurve;
    private float _stepDuration;
    private float _prevDisplacement;
    private bool _hasSyncedInitialDisplacement;
    private bool _isGroundedAtStart;
    private float _forwardDirection;
    private bool _forwardCompleted;
    private bool _hasBufferedAttackInput;

    private Collider2D _playerCollider;

    public PlayerAttackState(Player player, StateMachine stateMachine)
        : base(player, stateMachine)
    {
        _playerCollider = _player.GetComponent<Collider2D>();
    }

    public override void Enter()
    {
        base.Enter();

        _elapsedTime = 0f;
        _comboElapsedTime = 0f;
        _currentComboIndex = 0;
        _hasEnteredAttackAnimation = false;
        _hasBufferedAttackInput = false;
        _isGroundedAtStart = _player.IsGrounded;

        _rigidbody2D.linearVelocity = new Vector2(0f, _rigidbody2D.linearVelocity.y);

        SetupForwardMotion(0);

        _player.SetRunning(false);
        _player.TriggerAttack();
        _player.Attack.ExecuteAttackStep(0);
    }

    private void SetupForwardMotion(int index)
    {
        _forwardDirection = _player.transform.localScale.x > 0f ? 1f : -1f;
        _forwardCompleted = false;
        _prevDisplacement = 0f;
        _hasSyncedInitialDisplacement = false;
        _stepDuration = CalculateTotalStepDuration(index);

        if (_player.Attack.TryGetAttackData(index, out AttackData attackData) &&
            attackData.ForwardDisplacementCurve != null &&
            attackData.ForwardDisplacementCurve.length > 0)
        {
            _hasForwardMotion = true;
            _forwardDisplacementCurve = attackData.ForwardDisplacementCurve;
        }
        else
        {
            _hasForwardMotion = false;
            _forwardDisplacementCurve = null;
            _forwardCompleted = true;
        }
    }

    private void TransitionToCombo(int nextIndex)
    {
        _hasBufferedAttackInput = false;
        _currentComboIndex = nextIndex;
        _comboElapsedTime = 0f;
        StopForwardMovement();
        SetupForwardMotion(nextIndex);

        if (nextIndex == 1)
        {
            _player.TriggerAttack2();
        }
        else if (nextIndex == 2)
        {
            _player.TriggerAttack3();
        }
        else
        {
            _player.TriggerAttack();
        }

        _player.Attack.ExecuteAttackStep(nextIndex);
    }

    private bool IsInComboWindow(AnimatorStateInfo stateInfo, bool isRecovering)
    {
        if (stateInfo.IsName("Player_Attack_2nd_Ready") || stateInfo.IsName("Player_Attack_3rd_Ready"))
        {
            return false;
        }

        float comboStart = DefaultComboStartNormalizedTime;
        float comboEnd = DefaultComboEndNormalizedTime;

        if (_player.Attack.TryGetAttackData(_currentComboIndex, out AttackData attackData))
        {
            if (attackData.ComboStartNormalizedTime > 0f)
            {
                comboStart = attackData.ComboStartNormalizedTime;
            }
            if (attackData.ComboEndNormalizedTime > 0f)
            {
                comboEnd = attackData.ComboEndNormalizedTime;
            }
        }

        if (isRecovering)
        {
            return stateInfo.normalizedTime < comboEnd;
        }

        if (IsCurrentComboAttackMotion(stateInfo))
        {
            return stateInfo.normalizedTime >= comboStart;
        }

        return false;
    }

    public override void PhysicsExecute()
    {
        base.PhysicsExecute();

        if (!_hasForwardMotion || _forwardCompleted || _forwardDisplacementCurve == null) return;

        float progress = _stepDuration > 0.0001f
            ? Mathf.Clamp01(_comboElapsedTime / _stepDuration)
            : 1f;

        float targetDisplacement = _forwardDisplacementCurve.Evaluate(progress);

        if (!_hasSyncedInitialDisplacement)
        {
            _hasSyncedInitialDisplacement = true;
            _prevDisplacement = _forwardDisplacementCurve.Evaluate(0f);
        }

        float deltaDistance = targetDisplacement - _prevDisplacement;
        _prevDisplacement = targetDisplacement;

        if (deltaDistance > 0.0001f)
        {
            float safeDelta = CheckLedgeObstacle(deltaDistance);

            if (safeDelta > 0.0001f)
            {
                Vector2 newPos = _rigidbody2D.position + new Vector2(_forwardDirection * safeDelta, 0f);
                _rigidbody2D.MovePosition(newPos);
            }
        }
        else if (deltaDistance < -0.0001f)
        {
            Vector2 newPos = _rigidbody2D.position + new Vector2(_forwardDirection * deltaDistance, 0f);
            _rigidbody2D.MovePosition(newPos);
        }

        if (progress >= 1f)
        {
            StopForwardMovement();
        }
    }

    private float CheckLedgeObstacle(float desiredDistance)
    {
        if (!_isGroundedAtStart || _playerCollider == null || desiredDistance <= 0f)
        {
            return desiredDistance;
        }

        Bounds bounds = _playerCollider.bounds;
        float currentLeadingEdge = _forwardDirection > 0f ? bounds.max.x : bounds.min.x;
        float probeX = currentLeadingEdge + (_forwardDirection * (desiredDistance + LedgeSafetyMargin));
        var probeOrigin = new Vector2(probeX, bounds.min.y + LedgeRayOriginYOffset);

        RaycastHit2D hit = Physics2D.Raycast(probeOrigin, Vector2.down, LedgeRayDistance, Constants.Layers.GroundAllMask);
        if (hit.collider != null)
        {
            return desiredDistance;
        }

        float maxAllowedDelta = 0f;
        for (int i = 1; i <= LedgeCheckSteps; i++)
        {
            float testDelta = desiredDistance * (1f - (float)i / LedgeCheckSteps);
            if (testDelta <= 0.001f)
            {
                break;
            }

            float testX = currentLeadingEdge + (_forwardDirection * (testDelta + LedgeSafetyMargin));
            var testOrigin = new Vector2(testX, bounds.min.y + LedgeRayOriginYOffset);
            RaycastHit2D stepHit = Physics2D.Raycast(testOrigin, Vector2.down, LedgeRayDistance, Constants.Layers.GroundAllMask);
            if (stepHit.collider != null)
            {
                maxAllowedDelta = testDelta;
                break;
            }
        }

        return maxAllowedDelta;
    }

    private void StopForwardMovement()
    {
        if (_forwardCompleted) return;

        _forwardCompleted = true;
        _rigidbody2D.linearVelocity = new Vector2(0f, _rigidbody2D.linearVelocity.y);
    }

    public override void Execute()
    {
        base.Execute();

        _elapsedTime += Time.deltaTime;
        _comboElapsedTime += Time.deltaTime;

        if (_elapsedTime >= AttackSafetyTimeout)
        {
            EndAttack();
            return;
        }

        Animator animator = _player.Animator;
        if (animator != null)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            bool inTransition = animator.IsInTransition(0);
            AnimatorStateInfo nextInfo = inTransition ? animator.GetNextAnimatorStateInfo(0) : default;

            bool isCurrentAttackOrRecover = IsAttackOrRecoverState(stateInfo);
            bool isNextAttackOrRecover = inTransition && IsAttackOrRecoverState(nextInfo);

            if (isCurrentAttackOrRecover || isNextAttackOrRecover)
            {
                _hasEnteredAttackAnimation = true;
            }

            bool isCurrentComboRecover = IsCurrentComboRecoverState(stateInfo);

            // 콤보 공격 입력 감지 시 단일 예약 버퍼 활성화
            if (_currentComboIndex < 2 && _inputHandler.TryUseAttackInput())
            {
                _hasBufferedAttackInput = true;
            }

            // 예약된 입력이 있고, 전이 중이 아니며 콤보 조건을 충족했을 때만 다음 콤보로 전이
            if (_hasBufferedAttackInput && _currentComboIndex < 2 && !inTransition)
            {
                if (IsInComboWindow(stateInfo, isCurrentComboRecover))
                {
                    _hasBufferedAttackInput = false;
                    TransitionToCombo(_currentComboIndex + 1);
                    return;
                }
            }

            // 현재 진행 중인 콤보의 리커버 상태일 때만 후속 처리 (회피 캔슬 및 종료)
            if (isCurrentComboRecover)
            {
                if (stateInfo.normalizedTime >= DodgeCancelNormalizedTime)
                {
                    if (_inputHandler.TryUseDodgeInput() && _player.IsGrounded && _player.Movement.CanDodge)
                    {
                        _stateMachine.ChangeState(_player.DodgeState);
                        return;
                    }
                }

                if (!inTransition && stateInfo.normalizedTime >= 1.0f)
                {
                    EndAttack();
                    return;
                }
            }
            else if (_hasEnteredAttackAnimation && !isCurrentAttackOrRecover && !isNextAttackOrRecover)
            {
                EndAttack();
                return;
            }
        }
    }

    private bool IsCurrentComboRecoverState(AnimatorStateInfo stateInfo)
    {
        return _currentComboIndex switch
        {
            0 => stateInfo.IsName("Player_Attack_1st_Recover"),
            1 => stateInfo.IsName("Player_Attack_2nd_Recover"),
            2 => stateInfo.IsName("Player_Attack_3rd_Recover"),
            _ => false
        };
    }

    private bool IsCurrentComboAttackMotion(AnimatorStateInfo stateInfo)
    {
        return _currentComboIndex switch
        {
            0 => stateInfo.IsName("Player_Attack_1st") || stateInfo.IsName("Player_Attack") || stateInfo.IsName("Player_New_Attack_1st"),
            1 => stateInfo.IsName("Player_Attack_2nd_Ready") || stateInfo.IsName("Player_Attack_2nd"),
            2 => stateInfo.IsName("Player_Attack_3rd_Ready") || stateInfo.IsName("Player_Attack_3rd"),
            _ => false
        };
    }

    private bool IsAttackOrRecoverState(AnimatorStateInfo stateInfo)
    {
        if (stateInfo.IsTag("AttackRecover"))
        {
            return true;
        }

        return stateInfo.IsName("Player_Attack") ||
               stateInfo.IsName("Player_Attack_1st") ||
               stateInfo.IsName("Player_New_Attack_1st") ||
               stateInfo.IsName("Player_Attack_2nd") ||
               stateInfo.IsName("Player_Attack_3rd") ||
               stateInfo.IsName("Player_Attack_2nd_Ready") ||
               stateInfo.IsName("Player_Attack_3rd_Ready");
    }

    private void EndAttack()
    {
        _stateMachine.ChangeState(_player.IdleState);
    }

    public override void Exit()
    {
        base.Exit();

        _hasBufferedAttackInput = false;
        _player.Attack.StopAttack();
    }

    private float CalculateTotalStepDuration(int index)
    {
        if (index < 0 || index >= _cachedStepDurations.Length)
        {
            return 0.5f;
        }

        if (!_hasCachedStepDurations)
        {
            Animator animator = _player.Animator;
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                CacheStepDurations(animator.runtimeAnimatorController);
            }
            else
            {
                return GetFallbackStepDuration(index);
            }
        }

        return _cachedStepDurations[index];
    }

    private void CacheStepDurations(RuntimeAnimatorController controller)
    {
        AnimationClip[] clips = controller.animationClips;

        for (int index = 0; index < _cachedStepDurations.Length; index++)
        {
            string readyClipName = null;
            string attackClipName = null;

            if (index == 0)
            {
                attackClipName = "Player_Attack_1st";
            }
            else if (index == 1)
            {
                readyClipName = "Player_Attack_2nd_Ready";
                attackClipName = "Player_Attack_2nd";
            }
            else if (index == 2)
            {
                readyClipName = "Player_Attack_3rd_Ready";
                attackClipName = "Player_Attack_3rd";
            }

            float readyDuration = 0f;
            float attackDuration = 0f;

            if (clips != null)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    AnimationClip clip = clips[i];
                    if (clip == null) continue;

                    if (readyClipName != null && clip.name == readyClipName)
                    {
                        readyDuration = clip.length;
                    }
                    else if (attackClipName != null && (clip.name == attackClipName || (index == 0 && clip.name == "Player_Attack")))
                    {
                        attackDuration = clip.length;
                    }
                }
            }

            float totalDuration = readyDuration + attackDuration;
            _cachedStepDurations[index] = totalDuration > 0.0001f ? totalDuration : GetFallbackStepDuration(index);
        }

        _hasCachedStepDurations = true;
    }

    private float GetFallbackStepDuration(int index)
    {
        return index switch
        {
            0 => 0.5625f,
            1 => 0.375f + 0.4375f,
            2 => 0.5f + 1.25f,
            _ => 0.5f
        };
    }
}