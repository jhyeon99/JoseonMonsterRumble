using UnityEngine;

public enum PatrolYHeightType
{
    Center, // Y축 중앙 높이
    MaxY // Y축 최상단 높이
}

public class EnemyFlyingMove : EnemyMoveType
{
    private const float FlightAcceleration = 8f;
    private const float ChaseDistanceMargin = 1f;
    private const float CollisionSkin = 0.02f;
    private const float MinimumFlightSpeed = 0.0001f;
    private enum MeleeFlightMode { Hovering, Approaching }
    private MeleeFlightMode _meleeFlightMode;
    [Header("U자 돌진 설정")]

    [SerializeField] private float _uCurveBottomOffset = 0.4f;
    [SerializeField] private float _uCurveUpOffset = 1.5f;
    [SerializeField] private float _rotationSpeed = 15f;
    [Header("돌진 타격 감속 설정")]

    [SerializeField] private float _hitSlowDelay = 0.1f;
    [SerializeField, Range(0.1f, 1f)] private float _hitSlowRatio = 0.3f;
    [SerializeField] private float _hitSlowDuration = 0.25f;

    private float _slowDelayTimer = 0f;
    private float _slowDurationTimer = 0f;
    private bool _isSlowDelaying = false;
    private bool _isSlowed = false;
    private bool _hasTriggeredBottomSlow = false;
    private bool _isQuadraticCharging = false;
    private Vector2 _chargeStartPos;
    private Vector2 _chargeControlPos;
    private Vector2 _chargeEndPos;
    private float _chargeProgress = 0f;
    private float _chargeDuration = 1f;
    private EnemyRotation _enemyRotation;
    private EnemyFlip _enemyFlip;
    private FlyingMeleeAttackController _meleeAttack;

    [Header("호버링 및 원거리 이동 설정")]
    [SerializeField] private float _hoverAmplitude = 0.4f;
    [SerializeField] private float _hoverFrequency = 2.5f;
    [SerializeField, Range(0f, 1f)] private float _flyOverChance = 0.25f;
    private bool _isPerformingFlyOver = false;
    private float _flyOverTimer = 0f;

    [Header("패트롤 설정")]

    [SerializeField] private float _patrolWaitDuration = 1.0f; // 끝 지점 도달 후 대기 시간(초)
    [SerializeField] private PatrolYHeightType _patrolYHeight = PatrolYHeightType.Center;

    private bool _isMovingRight = true;
    private float _patrolWaitTimer = 0f;
    private bool _isPatrolWaiting = false;

    protected override void Awake()
    {
        base.Awake();
        _enemyRotation = GetComponent<EnemyRotation>();
        _enemyFlip = GetComponent<EnemyFlip>();
        _meleeAttack = GetComponent<FlyingMeleeAttackController>();
    }

    /// <summary>
    /// PatrolYHeightType 설정에 따라 허용되는 최대 Y축 좌표를 반환합니다.
    /// </summary>
    private float GetMaxAllowedY()
    {
        if (Enemy == null || Enemy.MoveArea == null) return float.MaxValue;

        Vector2 center = Enemy.MoveArea.StartPosition + Enemy.MoveArea.CenterOffset;
        float halfHeight = Enemy.MoveArea.AreaSize.y * 0.5f;

        return _patrolYHeight switch
        {
            PatrolYHeightType.MaxY => center.y + halfHeight,
            _ => center.y // Center 지정 시 Center 높이 이상 올라가지 못함
        };
    }

    public override void StartQuadraticCharge(Vector2 playerPos)
    {
        // 밀림 중 새 돌진이 시작되면 그 시점 이후의 이동만 궤적에 반영한다.
        ResetLightKnockbackOrigin();
        _chargeStartPos = Rigidbody.position;

        _chargeControlPos = playerPos + (Vector2.down * _uCurveBottomOffset);

        Vector2 toPlayer = playerPos - _chargeStartPos;
        _chargeEndPos = playerPos + new Vector2(toPlayer.x, Mathf.Abs(toPlayer.y) + _uCurveUpOffset);

        _chargeProgress = 0f;
        _chargeDuration = Mathf.Max(Enemy.Data.AttackDurationTime, 0.1f);
        _isQuadraticCharging = true;

        _hasTriggeredBottomSlow = false;
        _isSlowDelaying = false;
        _isSlowed = false;
    }

    public void ApplyHitSlowdown()
    {
        if (_isSlowDelaying || _isSlowed) return;

        _isSlowDelaying = true;
        _isSlowed = false;
        _slowDelayTimer = _hitSlowDelay;
        _slowDurationTimer = _hitSlowDuration;
    }

    protected override void HandleLightKnockbackEnd(Vector2 displacement)
    {
        if (!_isQuadraticCharging)
        {
            return;
        }

        // 저장된 궤적으로 되돌아가지 않고 밀린 위치에서 돌진을 이어 간다.
        _chargeStartPos += displacement;
        _chargeControlPos += displacement;
        _chargeEndPos += displacement;
    }

    public override void QuadraticChargePhysicsUpdate()
    {
        if (!_isQuadraticCharging) return;

        float currentMultiplier = 1f;

        if (_isSlowDelaying)
        {
            _slowDelayTimer -= Time.fixedDeltaTime;
            if (_slowDelayTimer <= 0f)
            {
                _isSlowDelaying = false;
                _isSlowed = true;
            }
        }

        if (_isSlowed)
        {
            _slowDurationTimer -= Time.fixedDeltaTime;
            if (_slowDurationTimer <= 0f)
            {
                _isSlowed = false;
            }
            else
            {
                currentMultiplier = _hitSlowRatio;
            }
        }

        float effectiveDeltaTime = Time.fixedDeltaTime * currentMultiplier;

        _chargeProgress += effectiveDeltaTime / _chargeDuration;
        float t = Mathf.Clamp01(_chargeProgress);

        Vector2 nextPos = (Mathf.Pow(1f - t, 2f) * _chargeStartPos) +
                          (2f * (1f - t) * t * _chargeControlPos) +
                          (Mathf.Pow(t, 2f) * _chargeEndPos);

        float maxY = GetMaxAllowedY();
        nextPos.y = Mathf.Min(nextPos.y, maxY);

        Rigidbody.MovePosition(nextPos);

        Vector2 tangent = 2f * (1f - t) * (_chargeControlPos - _chargeStartPos) +
                          2f * t * (_chargeEndPos - _chargeControlPos);

        if (!_hasTriggeredBottomSlow && tangent.y >= 0f)
        {
            _hasTriggeredBottomSlow = true;
            ApplyHitSlowdown();
        }

        if (tangent.sqrMagnitude > 0.0001f)
        {
            float moveAngle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
            float rotationOffset = -180f;

            var targetRotation = Quaternion.Euler(0f, 0f, moveAngle + rotationOffset);

            transform.rotation =
                Quaternion.Slerp(transform.rotation, targetRotation, effectiveDeltaTime * _rotationSpeed);
        }

        if (t >= 1.0f)
        {
            _isQuadraticCharging = false;
            RotateToPlayer();
        }
    }

    public override void StopQuadraticCharge()
    {
        _isQuadraticCharging = false;
        _isSlowDelaying = false;
        _isSlowed = false;
        _hasTriggeredBottomSlow = false;
        _slowDelayTimer = 0f;
        _slowDurationTimer = 0f;

        Rigidbody.linearVelocity = Vector2.zero;

        if (Enemy.MoveArea.IsPositionInChaseArea(Enemy.Target.position))
        {
            RotateToPlayer();
        }
        else
        {
            Enemy.transform.rotation = Quaternion.identity;
            Vector3 scale = Enemy.transform.localScale;
            scale.y = 1f;
            Enemy.transform.localScale = scale;
        }
    }

    public override void Move(Vector2 direction)
    {
        if (_meleeAttack != null)
        {
            _meleeAttack.TryMoveToPosition(Rigidbody.position + direction * Enemy.Data.MoveSpeed * Time.fixedDeltaTime,
                Enemy.Data.MoveSpeed);
            return;
        }
        Vector2 desiredVelocity = direction * Enemy.Data.MoveSpeed;
        Rigidbody.linearVelocity = GetAngleRestrictedVelocity(desiredVelocity, out bool isAllowed);
    }

    public override bool TryMoveToSearchPosition(Vector2 position)
    {
        if (_meleeAttack != null)
        {
            position = _meleeAttack.ClampPosition(position);
            return !_meleeAttack.IsAtPosition(position) && _meleeAttack.TryMoveToPosition(position, Enemy.Data.MoveSpeed);
        }
        position.y = Mathf.Min(position.y, GetMaxAllowedY());
        return base.TryMoveToSearchPosition(position);
    }

    public override void BackMove(Vector2 direction)
    {
        if (_meleeAttack != null)
        {
            Move(-direction);
            return;
        }
        if (Enemy != null && Enemy.Combat is EnemyRangedCombat)
        {
            float dirSign = direction.x >= 0f ? -1f : 1f;

            // 경계 접근 시 공간 재확보 (상승 및 중앙 회피)
            if (IsEndPosition(direction))
            {
                Vector2 reclaimVelocity = new Vector2(dirSign * 0.3f, 1.0f).normalized * Enemy.Data.MoveSpeed;
                Rigidbody.linearVelocity = GetAngleRestrictedVelocity(reclaimVelocity, out _);
                return;
            }

            // 간헐적 선회 기동 갱신
            _flyOverTimer -= Time.fixedDeltaTime;
            if (_flyOverTimer <= 0f)
            {
                _isPerformingFlyOver = Random.value < _flyOverChance;
                _flyOverTimer = 1.0f;
            }

            Vector2 desiredVelocity;
            if (_isPerformingFlyOver)
            {
                // 선회 기동: 대각선 위로 호를 그리며 이동
                desiredVelocity = new Vector2(dirSign * 0.8f, 0.9f).normalized * Enemy.Data.MoveSpeed;
            }
            else
            {
                // 기본 수평 후퇴
                desiredVelocity = new Vector2(dirSign * Enemy.Data.MoveSpeed, 0f);
            }

            Rigidbody.linearVelocity = GetAngleRestrictedVelocity(desiredVelocity, out _);
            return;
        }

        Vector2 defaultVelocity = -(direction * Enemy.Data.MoveSpeed);
        Rigidbody.linearVelocity = GetAngleRestrictedVelocity(defaultVelocity, out _);
    }

    public void HoverInPlace()
    {
        float hoverY = Mathf.Cos(Time.time * _hoverFrequency) * _hoverAmplitude;
        var hoverVelocity = new Vector2(0f, hoverY);
        if (_meleeAttack != null)
        {
            ApplyMeleeFlightVelocity(hoverVelocity);
            return;
        }
        Rigidbody.linearVelocity = GetAngleRestrictedVelocity(hoverVelocity, out _);
    }

    public void ResetMeleeFlight()
    {
        _meleeFlightMode = MeleeFlightMode.Hovering;
    }

    public void UpdateMeleeChaseFlight()
    {
        float distance = Enemy.GetDistanceToPlayer();
        if (distance > Enemy.Data.AttackDetectRange + ChaseDistanceMargin)
        {
            _meleeFlightMode = MeleeFlightMode.Approaching;
        }
        else if (distance <= Enemy.Data.AttackDetectRange)
        {
            _meleeFlightMode = MeleeFlightMode.Hovering;
        }
        if (_meleeFlightMode == MeleeFlightMode.Hovering)
        {
            HoverInPlace();
            return;
        }
        Vector2 offset = _meleeAttack.GetTargetPosition() + Vector2.up * _meleeAttack.Data.DiveHeight - Rigidbody.position;
        ApplyMeleeFlightVelocity(offset.normalized * Enemy.Data.MoveSpeed);
    }

    private void ApplyMeleeFlightVelocity(Vector2 desiredVelocity)
    {
        var velocity = Vector2.MoveTowards(Rigidbody.linearVelocity, desiredVelocity, FlightAcceleration * Time.fixedDeltaTime);
        velocity = CalculateSafeKnockbackVelocity(velocity);
        float distance = velocity.magnitude * Time.fixedDeltaTime;
        if (distance > MinimumFlightSpeed && Enemy.AirSensor != null)
        {
            float allowedDistance = Enemy.AirSensor.CalculateAllowedTravelDistance(velocity.normalized, distance, CollisionSkin);
            velocity *= allowedDistance / distance;
        }
        Rigidbody.linearVelocity = velocity;
        if (Enemy.Target != null)
        {
            _meleeAttack.FaceDirection(Enemy.GetDirectionToPlayer());
        }
    }

    public void MoveCircleHover(Vector2 direction)
    {
        float dirSign = direction.x >= 0f ? -1f : 1f;
        float hoverY = Mathf.Cos(Time.time * _hoverFrequency) * (_hoverAmplitude * 0.5f);
        var arcVelocity = new Vector2(dirSign * 0.6f * Enemy.Data.MoveSpeed, 0.7f * Enemy.Data.MoveSpeed + hoverY);
        Rigidbody.linearVelocity = GetAngleRestrictedVelocity(arcVelocity, out _);
    }

    public void MoveDisengage(Vector2 direction)
    {
        float dirSign = direction.x >= 0f ? -1f : 1f;
        float hoverY = Mathf.Cos(Time.time * _hoverFrequency) * _hoverAmplitude;
        var disengageVelocity = new Vector2(dirSign * Enemy.Data.MoveSpeed * 1.15f, hoverY);
        Rigidbody.linearVelocity = GetAngleRestrictedVelocity(disengageVelocity, out _);
    }

    public override void StopMove()
    {
        Rigidbody.linearVelocity = Vector2.zero;
        if (_meleeAttack != null)
        {
            _meleeAttack.ResetVisualTilt();
        }
    }

    public override Vector2 CalculateSafeKnockbackVelocity(Vector2 velocity)
    {
        if (_meleeAttack == null)
        {
            return base.CalculateSafeKnockbackVelocity(velocity);
        }
        Vector2 nextPosition = _meleeAttack.ClampPosition(Rigidbody.position + velocity * Time.fixedDeltaTime);
        Vector2 allowedVelocity = (nextPosition - Rigidbody.position) / Time.fixedDeltaTime;
        velocity.x = Mathf.Sign(velocity.x) * Mathf.Min(Mathf.Abs(velocity.x), Mathf.Max(0f, allowedVelocity.x * Mathf.Sign(velocity.x)));
        velocity.y = Mathf.Sign(velocity.y) * Mathf.Min(Mathf.Abs(velocity.y), Mathf.Max(0f, allowedVelocity.y * Mathf.Sign(velocity.y)));
        return velocity;
    }

    public override void KnockbackPhysicsUpdate(bool isHorizontalOnly)
    {
        base.KnockbackPhysicsUpdate(isHorizontalOnly);
        if (_meleeAttack != null)
        {
            Rigidbody.linearVelocity = CalculateSafeKnockbackVelocity(Rigidbody.linearVelocity);
        }
    }

    public override bool IsEndPosition(Vector2 direction)
    {
        if (Enemy == null || Enemy.MoveArea == null)
        {
            return false;
        }

        Vector2 center = Enemy.MoveArea.StartPosition + Enemy.MoveArea.CenterOffset;
        float halfWidth = Enemy.MoveArea.AreaSize.x * 0.5f;
        float margin = 1.2f;

        if (direction.x > 0f && Rigidbody.position.x <= center.x - halfWidth + margin)
        {
            return true;
        }
        if (direction.x < 0f && Rigidbody.position.x >= center.x + halfWidth - margin)
        {
            return true;
        }

        return false;
    }

    private Vector2 GetAngleRestrictedVelocity(Vector2 desiredVelocity, out bool isAllowed)
    {
        float maxY = GetMaxAllowedY();

        // 현재 최대 높이 이상에 위치해 있고, 상향(+Y) 이동을 시도할 경우 Y속도 차단
        if (Rigidbody.position.y >= maxY && desiredVelocity.y > 0f)
        {
            desiredVelocity.y = 0f;
        }

        if (Enemy.Target == null)
        {
            isAllowed = true;
            return desiredVelocity;
        }

        Vector2 playerPos = Enemy.Target.position;
        Vector2 relPos = Rigidbody.position - playerPos;
        float distance = relPos.magnitude;

        if (distance < 0.001f)
        {
            relPos = Vector2.right * 0.1f;
            distance = 0.1f;
        }

        float currentAngle = Mathf.Atan2(relPos.y, relPos.x) * Mathf.Rad2Deg;

        isAllowed = (currentAngle >= -Enemy.RightAngle && currentAngle <= Enemy.RightAngle) ||
                    (currentAngle >= Enemy.LeftAngle || currentAngle <= -Enemy.LeftAngle);

        if (isAllowed)
        {
            return desiredVelocity;
        }

        float targetAngle;

        if (currentAngle > Enemy.RightAngle && currentAngle < Enemy.LeftAngle)
        {
            targetAngle = relPos.x >= 0f ? Enemy.RightAngle : Enemy.LeftAngle;
        }
        else
        {
            targetAngle = relPos.x >= 0f ? -Enemy.RightAngle : -Enemy.LeftAngle;
        }

        float rad = targetAngle * Mathf.Deg2Rad;
        Vector2 targetOffset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * distance;
        Vector2 targetWorldPos = playerPos + targetOffset;

        // 제한 각도로 계산된 목표 위치의 Y 좌표도 상한선 제한
        targetWorldPos.y = Mathf.Min(targetWorldPos.y, maxY);

        Vector2 moveDir = (targetWorldPos - Rigidbody.position).normalized;
        Vector2 finalVelocity = moveDir * Enemy.Data.MoveSpeed;

        if (Rigidbody.position.y >= maxY && finalVelocity.y > 0f)
        {
            finalVelocity.y = 0f;
        }

        return finalVelocity;
    }

    public override void PatrolIdleMove()
    {
        if (Enemy.MoveArea == null) return;

        Vector2 center = Enemy.MoveArea.StartPosition + Enemy.MoveArea.CenterOffset;
        float halfWidth = Enemy.MoveArea.AreaSize.x * 0.5f;
        float halfHeight = Enemy.MoveArea.AreaSize.y * 0.5f;

        float targetY = _patrolYHeight switch
        {
            PatrolYHeightType.MaxY => center.y + halfHeight,
            _ => center.y
        };

        var leftPos = new Vector2(center.x - halfWidth, targetY);
        var rightPos = new Vector2(center.x + halfWidth, targetY);

        Vector2 targetPos = _isMovingRight ? rightPos : leftPos;
        if (_meleeAttack != null)
        {
            targetPos = _meleeAttack.ClampPosition(targetPos);
        }

        if (_isPatrolWaiting)
        {
            Rigidbody.linearVelocity = Vector2.zero;
            _patrolWaitTimer += Time.fixedDeltaTime;

            if (_patrolWaitTimer >= _patrolWaitDuration)
            {
                _isPatrolWaiting = false;
                _patrolWaitTimer = 0f;
                _isMovingRight = !_isMovingRight;
            }

            return;
        }

        if (_meleeAttack != null)
        {
            _meleeAttack.TryMoveToPosition(targetPos, Enemy.Data.MoveSpeed);
            if (_meleeAttack.IsAtPosition(targetPos))
            {
                _isPatrolWaiting = true;
                _patrolWaitTimer = 0f;
            }
            return;
        }
        Vector2 moveVector = targetPos - Rigidbody.position;
        HandleFlip(moveVector);

        var nextPos =
            Vector2.MoveTowards(Rigidbody.position, targetPos, Enemy.Data.MoveSpeed * Time.fixedDeltaTime);

        nextPos.y = Mathf.Min(nextPos.y, GetMaxAllowedY());
        Rigidbody.MovePosition(nextPos);

        if (Vector2.Distance(Rigidbody.position, targetPos) < 0.1f)
        {
            _isPatrolWaiting = true;
            _patrolWaitTimer = 0f;
        }
    }

    public override void MoveToInitialPosition()
    {
        Vector2 startPos = Enemy.MoveArea.StartPosition;
        if (_meleeAttack != null)
        {
            _meleeAttack.TryMoveToPosition(startPos, Enemy.Data.MoveSpeed);
            return;
        }
        Vector2 direction = (startPos - Rigidbody.position).normalized;

        Vector2 velocity = direction * Enemy.Data.MoveSpeed;
        if (Rigidbody.position.y >= GetMaxAllowedY() && velocity.y > 0f)
        {
            velocity.y = 0f;
        }

        Rigidbody.linearVelocity = velocity;
        HandleFlip(direction);
    }

    private void HandleFlip(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) < 0.01f) return;

        Vector3 scale = transform.localScale;
        if (direction.x > 0f)
        {
            scale.x = Mathf.Abs(scale.x);
        }
        else if (direction.x < 0f)
        {
            scale.x = -Mathf.Abs(scale.x);
        }

        transform.localScale = scale;
    }

    private void RotateToPlayer()
    {
        if (_enemyRotation != null)
        {
            _enemyRotation.ForceRotateToPlayer();
        }

        if (_enemyFlip != null)
        {
            _enemyFlip.Flip();
        }
    }
}
