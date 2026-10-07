using UnityEngine;

public enum PatrolYHeightType
{
    Center, // Y축 중앙 높이
    MaxY // Y축 최상단 높이
}

public class EnemyFlyingMove : EnemyMoveType
{
    [Header("U자 돌진 설정")]

    [SerializeField] private float _uCurveBottomOffset = 0.4f;
    [SerializeField] private float _uCurveUpOffset = 1.5f;
    [SerializeField] private float _rotationSpeed = 15f;
    private bool _isQuadraticCharging = false;
    private Vector2 _chargeStartPos;
    private Vector2 _chargeControlPos;
    private Vector2 _chargeEndPos;
    private float _chargeProgress = 0f;
    private float _chargeDuration = 1f;
    [Header("패트롤 설정")]

    [SerializeField] private float _patrolWaitDuration = 1.0f; // 끝 지점 도달 후 대기 시간(초)
    [SerializeField] private PatrolYHeightType _patrolYHeight = PatrolYHeightType.Center;

    private bool _isMovingRight = true;
    private float _patrolWaitTimer = 0f;
    private bool _isPatrolWaiting = false;

    public override void StartQuadraticCharge(Vector2 playerPos)
    {
        _chargeStartPos = Rigidbody.position;

        _chargeControlPos = playerPos + (Vector2.down * _uCurveBottomOffset);

        Vector2 toPlayer = playerPos - _chargeStartPos;
        _chargeEndPos = playerPos + new Vector2(toPlayer.x, Mathf.Abs(toPlayer.y) + _uCurveUpOffset);

        _chargeProgress = 0f;
        _chargeDuration = Mathf.Max(Enemy.Data.AttackDurationTime, 0.1f);
        _isQuadraticCharging = true;
    }

    public override void QuadraticChargePhysicsUpdate()
    {
        if (!_isQuadraticCharging) return;

        _chargeProgress += Time.fixedDeltaTime / _chargeDuration;
        float t = Mathf.Clamp01(_chargeProgress);

        Vector2 nextPos = (Mathf.Pow(1f - t, 2f) * _chargeStartPos) +
                          (2f * (1f - t) * t * _chargeControlPos) +
                          (Mathf.Pow(t, 2f) * _chargeEndPos);

        Rigidbody.MovePosition(nextPos);
        Vector2 tangent = 2f * (1f - t) * (_chargeControlPos - _chargeStartPos) +
                          2f * t * (_chargeEndPos - _chargeControlPos);

        if (tangent.sqrMagnitude > 0.0001f)
        {
            float moveAngle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
            float rotationOffset = -180f;

            var targetRotation = Quaternion.Euler(0f, 0f, moveAngle + rotationOffset);

            transform.rotation =
                Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * _rotationSpeed);
        }

        if (t >= 1.0f)
        {
            _isQuadraticCharging = false;
        }
    }

    public override void StopQuadraticCharge()
    {
        _isQuadraticCharging = false;
        Rigidbody.linearVelocity = Vector2.zero;
    }

    public override void Move(Vector2 direction)
    {
        Vector2 desiredVelocity = direction * Enemy.Data.MoveSpeed;
        Rigidbody.linearVelocity = GetAngleRestrictedVelocity(desiredVelocity, out bool isAllowed);
    }

    public override void BackMove(Vector2 direction)
    {
        // 후퇴 속도 (진행 방향의 반대)
        Vector2 desiredVelocity = -(direction * Enemy.Data.MoveSpeed);
        Rigidbody.linearVelocity = GetAngleRestrictedVelocity(desiredVelocity, out _);
    }

    public override void StopMove()
    {
        Rigidbody.linearVelocity = Vector2.zero;
    }

    private Vector2 GetAngleRestrictedVelocity(Vector2 desiredVelocity, out bool isAllowed)
    {
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

        // 이동 가능한 각도의 위치로 목표 지점 계산 및 속도 반환
        float rad = targetAngle * Mathf.Deg2Rad;
        Vector2 targetOffset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * distance;
        Vector2 targetWorldPos = playerPos + targetOffset;

        Vector2 moveDir = (targetWorldPos - Rigidbody.position).normalized;
        return moveDir * Enemy.Data.MoveSpeed;
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

        // MoveArea Y축 중앙 높이 기준 좌/우 양 끝 지점 계산
        var leftPos = new Vector2(center.x - halfWidth, targetY);
        var rightPos = new Vector2(center.x + halfWidth, targetY);

        Vector2 targetPos = _isMovingRight ? rightPos : leftPos;

        // 지점 도달 후 대기 상태 처리
        if (_isPatrolWaiting)
        {
            Rigidbody.linearVelocity = Vector2.zero;
            _patrolWaitTimer += Time.fixedDeltaTime;

            if (_patrolWaitTimer >= _patrolWaitDuration)
            {
                _isPatrolWaiting = false;
                _patrolWaitTimer = 0f;
                _isMovingRight = !_isMovingRight; // 대기 끝난 후 방향 전환 (우 <-> 좌)
            }

            return;
        }

        Vector2 moveVector = targetPos - Rigidbody.position;
        HandleFlip(moveVector);

        var nextPos =
            Vector2.MoveTowards(Rigidbody.position, targetPos, Enemy.Data.MoveSpeed * Time.fixedDeltaTime);
        Rigidbody.MovePosition(nextPos);

        // 양 끝 지점 도달 판정
        if (Vector2.Distance(Rigidbody.position, targetPos) < 0.1f)
        {
            _isPatrolWaiting = true;
            _patrolWaitTimer = 0f;
        }
    }

    public override void MoveToInitialPosition()
    {
        Vector2 startPos = Enemy.MoveArea.StartPosition;
        Vector2 direction = (startPos - Rigidbody.position).normalized;

        Rigidbody.linearVelocity = direction * Enemy.Data.MoveSpeed;
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
}
