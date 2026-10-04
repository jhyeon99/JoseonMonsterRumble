using UnityEngine;

public class PlayerAttackState : PlayerBaseState
{
    private float _elapsedTime;
    private float _comboElapsedTime;
    private int _currentComboIndex;
    private RigidbodyConstraints2D _originalConstraints;
    private bool _hasEnteredAttackAnimation;

    private bool _hasForwardMotion;
    private bool _useDisplacementCurve;
    private AnimationCurve _forwardDisplacementCurve;
    private float _readyDurationRatio;
    private float _prevDisplacement;
    private bool _hasSyncedInitialDisplacement;
    private bool _isGroundedAtStart;
    private float _forwardDelay;
    private float _forwardSpeed;
    private float _forwardDuration;
    private float _forwardDirection;
    private bool _isForwarding;
    private bool _forwardCompleted;

    private readonly RaycastHit2D[] _wallHitResults = new RaycastHit2D[4];
    private ContactFilter2D _wallContactFilter;
    private Collider2D _playerCollider;

    public PlayerAttackState(Player player, StateMachine stateMachine)
        : base(player, stateMachine)
    {
        _playerCollider = _player.GetComponent<Collider2D>();
        _wallContactFilter = new ContactFilter2D();
        _wallContactFilter.useTriggers = false;
        _wallContactFilter.SetLayerMask(Constants.Layers.GroundAllMask);
    }

    public override void Enter()
    {
        base.Enter();

        _elapsedTime = 0f;
        _comboElapsedTime = 0f;
        _currentComboIndex = 0;
        _hasEnteredAttackAnimation = false;
        _isGroundedAtStart = _player.IsGrounded;

        _originalConstraints = _rigidbody2D.constraints;
        _rigidbody2D.constraints = _originalConstraints | RigidbodyConstraints2D.FreezePositionX;
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

        if (_player.Attack.TryGetAttackData(index, out AttackData attackData))
        {
            if (attackData.ReadyDurationRatio > 0f)
            {
                _readyDurationRatio = attackData.ReadyDurationRatio;
            }
            else
            {
                _readyDurationRatio = CalculateDefaultReadyRatio(index);
            }

            if (attackData.ForwardDisplacementCurve != null && attackData.ForwardDisplacementCurve.length > 0)
            {
                _hasForwardMotion = true;
                _useDisplacementCurve = true;
                _forwardDisplacementCurve = attackData.ForwardDisplacementCurve;
                _isForwarding = false;
            }
            else if (attackData.ForwardSpeed > 0f && attackData.ForwardDuration > 0f)
            {
                _hasForwardMotion = true;
                _useDisplacementCurve = false;
                _forwardDelay = attackData.ForwardDelay;
                _forwardSpeed = attackData.ForwardSpeed;
                _forwardDuration = attackData.ForwardDuration;
                _isForwarding = false;
            }
            else
            {
                _hasForwardMotion = false;
                _useDisplacementCurve = false;
                _forwardCompleted = true;
            }
        }
        else
        {
            _hasForwardMotion = false;
            _useDisplacementCurve = false;
            _forwardCompleted = true;
        }
    }

    private void TransitionToCombo(int nextIndex)
    {
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

        float comboStart = 0.7f;
        float comboEnd = 0.5f;

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

        if (IsActualAttackMotion(stateInfo))
        {
            return stateInfo.normalizedTime >= comboStart;
        }

        return false;
    }

    public override void PhysicsExecute()
    {
        base.PhysicsExecute();

        if (!_hasForwardMotion || _forwardCompleted) return;

        if (_useDisplacementCurve)
        {
            Animator animator = _player.Animator;
            if (animator == null) return;

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

            float progress = CalculateComboProgress(stateInfo);
            if (progress < 0f)
            {
                return;
            }

            float targetDisplacement = _forwardDisplacementCurve.Evaluate(progress);

            if (!_hasSyncedInitialDisplacement)
            {
                _hasSyncedInitialDisplacement = true;
                _prevDisplacement = targetDisplacement;
                return;
            }

            float deltaDistance = targetDisplacement - _prevDisplacement;
            _prevDisplacement = targetDisplacement;

            if (deltaDistance > 0.0001f)
            {
                float safeDelta = CheckWallObstacle(deltaDistance);
                safeDelta = CheckLedgeObstacle(safeDelta);

                if (safeDelta > 0.0001f)
                {
                    if (!_isForwarding)
                    {
                        _isForwarding = true;
                        _rigidbody2D.constraints = _originalConstraints;
                    }

                    Vector2 newPos = _rigidbody2D.position + new Vector2(_forwardDirection * safeDelta, 0f);
                    _rigidbody2D.MovePosition(newPos);
                }
            }
            else if (deltaDistance < -0.0001f)
            {
                if (!_isForwarding)
                {
                    _isForwarding = true;
                    _rigidbody2D.constraints = _originalConstraints;
                }

                Vector2 newPos = _rigidbody2D.position + new Vector2(_forwardDirection * deltaDistance, 0f);
                _rigidbody2D.MovePosition(newPos);
            }
            else if (progress >= 0.99f)
            {
                StopForwardMovement();
            }
        }
        else
        {
            if (_comboElapsedTime >= _forwardDelay && _comboElapsedTime < _forwardDelay + _forwardDuration)
            {
                if (!_isForwarding)
                {
                    _isForwarding = true;
                    _rigidbody2D.constraints = _originalConstraints;
                }

                _rigidbody2D.linearVelocity = new Vector2(_forwardDirection * _forwardSpeed, _rigidbody2D.linearVelocity.y);
            }
            else if (_isForwarding || _comboElapsedTime >= _forwardDelay + _forwardDuration)
            {
                StopForwardMovement();
            }
        }
    }

    private float CalculateComboProgress(AnimatorStateInfo stateInfo)
    {
        bool isReady = false;
        bool isActualAttack = false;

        if (_currentComboIndex == 0)
        {
            isActualAttack = stateInfo.IsName("Player_Attack") ||
                             stateInfo.IsName("Player_Attack_1st") ||
                             stateInfo.IsName("Player_New_Attack_1st");
        }
        else if (_currentComboIndex == 1)
        {
            isReady = stateInfo.IsName("Player_Attack_2nd_Ready");
            isActualAttack = stateInfo.IsName("Player_Attack_2nd");
        }
        else if (_currentComboIndex == 2)
        {
            isReady = stateInfo.IsName("Player_Attack_3rd_Ready");
            isActualAttack = stateInfo.IsName("Player_Attack_3rd");
        }

        if (!isReady && !isActualAttack)
        {
            return -1f;
        }

        float normalizedTime = Mathf.Clamp01(stateInfo.normalizedTime);

        if (isReady && _readyDurationRatio > 0f)
        {
            return normalizedTime * _readyDurationRatio;
        }

        if (isActualAttack)
        {
            if (_readyDurationRatio > 0f)
            {
                return _readyDurationRatio + normalizedTime * (1.0f - _readyDurationRatio);
            }

            return normalizedTime;
        }

        return -1f;
    }

    private float CheckWallObstacle(float desiredDistance)
    {
        if (_playerCollider == null) return desiredDistance;

        var direction = new Vector2(_forwardDirection, 0f);
        int hitCount = _playerCollider.Cast(direction, _wallContactFilter, _wallHitResults, desiredDistance + 0.05f);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = _wallHitResults[i];
            if (hit.collider == null || hit.collider == _playerCollider) continue;

            if (Mathf.Abs(hit.normal.x) > 0.5f)
            {
                float safeDist = Mathf.Max(0f, hit.distance - 0.02f);
                return Mathf.Min(desiredDistance, safeDist);
            }
        }

        return desiredDistance;
    }

    private float CheckLedgeObstacle(float desiredDistance)
    {
        if (!_isGroundedAtStart || _playerCollider == null || desiredDistance <= 0f)
        {
            return desiredDistance;
        }

        Bounds bounds = _playerCollider.bounds;
        const float safetyMargin = 0.1f;
        const float rayDistance = 0.6f;
        float currentLeadingEdge = _forwardDirection > 0f ? bounds.max.x : bounds.min.x;
        float probeX = currentLeadingEdge + (_forwardDirection * (desiredDistance + safetyMargin));
        var probeOrigin = new Vector2(probeX, bounds.min.y + 0.1f);

        RaycastHit2D hit = Physics2D.Raycast(probeOrigin, Vector2.down, rayDistance, Constants.Layers.GroundAllMask);
        if (hit.collider != null)
        {
            return desiredDistance;
        }

        float maxAllowedDelta = 0f;
        const int steps = 5;
        for (int i = 1; i <= steps; i++)
        {
            float testDelta = desiredDistance * (1f - (float)i / steps);
            if (testDelta <= 0.001f)
            {
                break;
            }

            float testX = currentLeadingEdge + (_forwardDirection * (testDelta + safetyMargin));
            var testOrigin = new Vector2(testX, bounds.min.y + 0.1f);
            RaycastHit2D stepHit = Physics2D.Raycast(testOrigin, Vector2.down, rayDistance, Constants.Layers.GroundAllMask);
            if (stepHit.collider != null)
            {
                maxAllowedDelta = testDelta;
                break;
            }
        }

        return maxAllowedDelta;
    }

    private bool IsActualAttackMotion(AnimatorStateInfo stateInfo)
    {
        return stateInfo.IsName("Player_Attack") ||
               stateInfo.IsName("Player_Attack_1st") ||
               stateInfo.IsName("Player_New_Attack_1st") ||
               stateInfo.IsName("Player_Attack_2nd") ||
               stateInfo.IsName("Player_Attack_3rd");
    }

    private void StopForwardMovement()
    {
        if (_forwardCompleted) return;

        _isForwarding = false;
        _forwardCompleted = true;
        _rigidbody2D.constraints = _originalConstraints | RigidbodyConstraints2D.FreezePositionX;
        _rigidbody2D.linearVelocity = new Vector2(0f, _rigidbody2D.linearVelocity.y);
    }

    public override void Execute()
    {
        base.Execute();

        _elapsedTime += Time.deltaTime;
        _comboElapsedTime += Time.deltaTime;

        const float safetyTimeout = 5.0f;
        if (_elapsedTime >= safetyTimeout)
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

            bool isRecovering = stateInfo.IsTag("AttackRecover");
            bool isTransitioningToRecover = inTransition && nextInfo.IsTag("AttackRecover");

            if (isCurrentAttackOrRecover || isNextAttackOrRecover)
            {
                _hasEnteredAttackAnimation = true;
            }

            // 타격 진행도 0.7 이상 또는 리커버 진행도 0.5 미만 구간에서 다음 콤보 입력 허용
            if (_currentComboIndex < 2 && IsInComboWindow(stateInfo, isRecovering))
            {
                if (_inputHandler.TryUseAttackInput())
                {
                    TransitionToCombo(_currentComboIndex + 1);
                    return;
                }
            }

            // 신규 타격 애니메이션으로 전환 중일 때는 이전 리커버 상태로 인한 강제 정지 방지
            bool isTransitioningToNewAttack = inTransition && (nextInfo.IsName("Player_Attack_2nd_Ready") ||
                                                              nextInfo.IsName("Player_Attack_2nd") ||
                                                              nextInfo.IsName("Player_Attack_3rd_Ready") ||
                                                              nextInfo.IsName("Player_Attack_3rd"));

            if (!isTransitioningToNewAttack && (isRecovering || isTransitioningToRecover))
            {
                if (_isForwarding || !_forwardCompleted)
                {
                    StopForwardMovement();
                }

                if (isRecovering && stateInfo.normalizedTime >= 0.2f)
                {
                    if (_inputHandler.TryUseDodgeInput() && _player.IsGrounded && _player.Movement.CanDodge)
                    {
                        _stateMachine.ChangeState(_player.DodgeState);
                        return;
                    }
                }

                if (isRecovering && !inTransition && stateInfo.normalizedTime >= 1.0f)
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

        _rigidbody2D.constraints = _originalConstraints;
        _player.Attack.StopAttack();
    }

    private float CalculateDefaultReadyRatio(int index)
    {
        if (index == 0) return 0f;

        string readyClipName = index == 1 ? "Player_Attack_2nd_Ready" : "Player_Attack_3rd_Ready";
        string attackClipName = index == 1 ? "Player_Attack_2nd" : "Player_Attack_3rd";

        float readyDuration = 0f;
        float attackDuration = 0f;

        Animator animator = _player.Animator;
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip == null) continue;

                if (clip.name == readyClipName)
                {
                    readyDuration = clip.length;
                }
                else if (clip.name == attackClipName)
                {
                    attackDuration = clip.length;
                }
            }
        }

        float totalDuration = readyDuration + attackDuration;
        if (totalDuration > 0.0001f)
        {
            return readyDuration / totalDuration;
        }

        return index == 1 ? 0.4615f : (index == 2 ? 0.2857f : 0f);
    }
}