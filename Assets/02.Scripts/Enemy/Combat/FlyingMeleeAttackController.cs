using UnityEngine;

/// <summary>공중 근접 공격의 설정·물리 이동·표현 연결. 공격 순서는 전용 FSM이 관리한다.</summary>
[RequireComponent(typeof(EnemyMeleeCombat), typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyAirSensor))]
public sealed class FlyingMeleeAttackController : MonoBehaviour
{
    private const int AnimationLayer = 0;
    private const float CollisionSkin = 0.02f;
    private const float PositionTolerance = 0.1f;
    private const float DirectionTolerance = 0.0001f;
    private const float VisualTurnSpeed = 120f;
    private const float HorizontalDirectionTolerance = 0.01f;
    private const float TargetUpperBodyRatio = 0.5f;
    private const float FlightTiltSmoothTime = 0.25f;
    private const float FlightTiltTurnSpeed = 35f;
    private const float FlightVelocityResponse = 5f;
    private const float FlightSpeedDeadZone = 0.5f;
    private const float ForwardSpeedTilt = 0.8f;
    private const float SpeedChangeTilt = 1.5f;
    private const float VerticalSpeedTilt = 2f;
    private static readonly int HitTagHash = Animator.StringToHash("Hit");

    [SerializeField] private FlyingMeleeAttackData _data;
    [SerializeField] private Animator _animator;
    [SerializeField] private Collider2D _bodyCollider;
    [SerializeField] private EnemyContactDamage _contactDamage;
    [SerializeField, Range(0f, 30f)] private float _maxVisualTilt = 15f;
    [SerializeField, Range(0f, 15f)] private float _maxFlightVisualTilt = 7f;

    [Header("애니메이션 표현 상태")]
    [SerializeField] private string _idleState = "Base Layer.inmyunjo_Idle";
    [SerializeField] private string _prepareState = "Base Layer.inmyunjo_AtkReady";
    [SerializeField] private string _strikeState = "Base Layer.inmyunjo_Atk";
    [SerializeField] private string _recoverState = "Base Layer.inmyunjo_AtkEnd";

    private Enemy _enemy;
    private EnemyMeleeCombat _combat;
    private Rigidbody2D _rigidbody;
    private Transform _target;
    private Collider2D _targetCollider;
    private EnemyAirSensor _airSensor;
    private EnemyFlyingMove _flight;
    private Collider2D _contactCollider;
    private Vector2 _bodyPadding;
    private Transform _visualRoot;
    private FlyingMeleeAttackPhase _visualPhase;
    private Vector2 _smoothedFlightVelocity;
    private float _flightTiltVelocity;
    private int _idleHash;
    private int _prepareHash;
    private int _strikeHash;
    private int _recoverHash;
    private bool _isConfigured;
    private bool _wasContactDamageEnabled;
    private bool _isContactDamageSuppressed;

    public FlyingMeleeAttackData Data => _data;

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
        _combat = GetComponent<EnemyMeleeCombat>();
        _rigidbody = GetComponent<Rigidbody2D>();
        _airSensor = GetComponent<EnemyAirSensor>();
        _flight = GetComponent<EnemyFlyingMove>();
        _contactCollider = _contactDamage != null ? _contactDamage.GetComponent<Collider2D>() : null;
        _idleHash = Animator.StringToHash(_idleState);
        _prepareHash = Animator.StringToHash(_prepareState);
        _strikeHash = Animator.StringToHash(_strikeState);
        _recoverHash = Animator.StringToHash(_recoverState);
        _isConfigured = _enemy != null && _data != null && _airSensor != null && _flight != null && _bodyCollider != null && !_bodyCollider.isTrigger;
        if (!_isConfigured)
        {
            Debug.LogError("FlyingMeleeAttackController: Enemy, 공중 센서, 공격 SO, 몸체 Collider 참조를 확인하세요.", this);
            return;
        }

        // 원본은 오른쪽을 바라보는 수평 자세다. 몸체를 뒤집거나 경로 각도로 회전하지 않는다.
        transform.rotation = Quaternion.identity;
        Vector3 scale = transform.localScale;
        scale.y = Mathf.Abs(scale.y);
        transform.localScale = scale;
        _visualRoot = _animator != null ? _animator.transform : null;
        Bounds bodyBounds = _bodyCollider.bounds;
        Vector2 centerOffset = (Vector2)bodyBounds.center - _rigidbody.position;
        _bodyPadding = (Vector2)bodyBounds.extents + new Vector2(Mathf.Abs(centerOffset.x), Mathf.Abs(centerOffset.y));
    }

    private void LateUpdate()
    {
        if (!_isConfigured || _visualRoot == null || Time.deltaTime <= 0f)
        {
            return;
        }
        EnemyState state = _enemy.StateMachine.CurrentState;
        AnimatorStateInfo animationState = _animator.GetCurrentAnimatorStateInfo(AnimationLayer);
        bool isReaction = state is DeadState || state is KnockbackState || _enemy.IsGroggy || animationState.tagHash == HitTagHash;
        bool isFlight = !isReaction && animationState.fullPathHash == _idleHash &&
            (_visualPhase == FlyingMeleeAttackPhase.Completed || _visualPhase == FlyingMeleeAttackPhase.Hovering ||
                _visualPhase == FlyingMeleeAttackPhase.Positioning);
        if (!isFlight)
        {
            _smoothedFlightVelocity = Vector2.zero;
            _flightTiltVelocity = 0f;
            // 돌진은 기존 경로 기울기를 유지하고, 피격·사망 외형에는 비행 기울기를 남기지 않는다.
            if (isReaction)
            {
                _visualRoot.localRotation = Quaternion.identity;
            }
            return;
        }

        Vector2 velocity = _rigidbody.linearVelocity;
        float facing = Mathf.Sign(transform.localScale.x);
        float speedChange = (velocity.x - _smoothedFlightVelocity.x) * facing;
        float forwardSpeed = Mathf.Sign(velocity.x) * Mathf.Max(0f, Mathf.Abs(velocity.x) - FlightSpeedDeadZone) * facing;
        float verticalSpeed = Mathf.Sign(velocity.y) * Mathf.Max(0f, Mathf.Abs(velocity.y) - FlightSpeedDeadZone);
        float tilt = Mathf.Clamp(-forwardSpeed * ForwardSpeedTilt - speedChange * SpeedChangeTilt +
            verticalSpeed * VerticalSpeedTilt, -_maxFlightVisualTilt, _maxFlightVisualTilt);
        _smoothedFlightVelocity = Vector2.Lerp(_smoothedFlightVelocity, velocity,
            1f - Mathf.Exp(-FlightVelocityResponse * Time.deltaTime));
        float angle = Mathf.SmoothDampAngle(_visualRoot.localEulerAngles.z, tilt, ref _flightTiltVelocity,
            FlightTiltSmoothTime, FlightTiltTurnSpeed, Time.deltaTime);
        _visualRoot.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    public bool CanStartAttack()
    {
        if (!isActiveAndEnabled || !_isConfigured || _enemy.Target == null || _enemy.MoveArea == null ||
            _enemy.GetDistanceToPlayer() > _enemy.Data.AttackDetectRange)
        {
            return false;
        }

        Vector2 targetPosition = GetTargetPosition();
        return (ClampPosition(targetPosition) - targetPosition).sqrMagnitude <= DirectionTolerance &&
            _airSensor.HasLineOfSight(targetPosition);
    }

    public Vector2 GetTargetPosition()
    {
        if (_target != _enemy.Target)
        {
            _target = _enemy.Target;
            _targetCollider = _target != null ? _target.GetComponent<Collider2D>() : null;
        }
        return _targetCollider != null ? (Vector2)_targetCollider.bounds.center : (Vector2)_target.position;
    }

    public Vector2 ClampPosition(Vector2 position)
    {
        Vector2 center = _enemy.MoveArea.StartPosition + _enemy.MoveArea.CenterOffset;
        Vector2 halfSize = _enemy.MoveArea.AreaSize * 0.5f;
        halfSize.x = Mathf.Max(0f, halfSize.x - _bodyPadding.x);
        halfSize.y = Mathf.Max(0f, halfSize.y - _bodyPadding.y);
        return new Vector2(Mathf.Clamp(position.x, center.x - halfSize.x, center.x + halfSize.x),
            Mathf.Clamp(position.y, center.y - halfSize.y, center.y + halfSize.y));
    }

    public float GetHorizontalFlightHeight()
    {
        Vector2 targetPosition = GetTargetPosition();
        float targetHeight = targetPosition.y;
        if (_targetCollider != null)
        {
            targetHeight += _targetCollider.bounds.extents.y * TargetUpperBodyRatio;
        }
        // 좁은 물리 몸체뿐 아니라 접촉 판정의 아래쪽도 상체 높이 위로 확보한다.
        float lowestBodyY = _bodyCollider.bounds.min.y;
        if (_contactCollider != null)
        {
            lowestBodyY = Mathf.Min(lowestBodyY, _contactCollider.bounds.min.y);
        }
        return targetHeight + _rigidbody.position.y - lowestBodyY + CollisionSkin;
    }

    public float CalculateAllowedPrepareRetreatDistance(Vector2 direction)
    {
        Vector2 start = _rigidbody.position;
        Vector2 end = ClampPosition(start + direction * _data.PrepareRetreatDistance);
        Vector2 available = end - start;
        float distance = _data.PrepareRetreatDistance;
        if (Mathf.Abs(direction.x) > DirectionTolerance)
        {
            distance = Mathf.Min(distance, Mathf.Max(0f, available.x / direction.x));
        }
        if (Mathf.Abs(direction.y) > DirectionTolerance)
        {
            distance = Mathf.Min(distance, Mathf.Max(0f, available.y / direction.y));
        }
        return _airSensor.CalculateAllowedTravelDistance(direction, distance, CollisionSkin,
            Mathf.Abs(direction.y) <= DirectionTolerance);
    }

    public bool IsAtPosition(Vector2 position)
    {
        return (_rigidbody.position - position).sqrMagnitude <= PositionTolerance * PositionTolerance;
    }

    public void UpdateChaseFlight()
    {
        _flight.UpdateMeleeChaseFlight();
    }

    public bool CanTravelStraight(Vector2 start, Vector2 end)
    {
        return (ClampPosition(end) - end).sqrMagnitude <= DirectionTolerance &&
            _airSensor.IsFlightPathClear(start, end, CollisionSkin, Mathf.Abs(end.y - start.y) <= DirectionTolerance);
    }

    public bool TryMoveToPosition(Vector2 position)
    {
        return TryMoveToPosition(position, _data.PositioningSpeed);
    }

    public bool TryMoveToPosition(Vector2 position, float speed)
    {
        position = ClampPosition(position);
        position = Vector2.MoveTowards(_rigidbody.position, position, speed * Time.fixedDeltaTime);
        // 공격 진입 공간을 확보하는 동안에도 표적을 주시한다. 추적 직후 뒤돌았다 다시 도는 것을 막는다.
        Vector2 facingDirection = _enemy.Target != null ? GetTargetPosition() - _rigidbody.position : position - _rigidbody.position;
        return TryMove(position, facingDirection);
    }

    public bool TryMove(Vector2 position, Vector2 facingDirection)
    {
        Vector2 clampedPosition = ClampPosition(position);
        float positionError = (clampedPosition - position).sqrMagnitude;
        float currentError = (ClampPosition(_rigidbody.position) - _rigidbody.position).sqrMagnitude;
        // 순찰 끝에서 공격에 진입했을 때 경계 안으로 순간 이동하지 않고 같은 속도로 복귀한다.
        if (positionError > DirectionTolerance && positionError >= currentError)
        {
            return false;
        }
        Vector2 movement = position - _rigidbody.position;
        float distance = movement.magnitude;
        float allowedDistance = distance;
        if (distance > DirectionTolerance)
        {
            bool isHorizontalAttack = (_visualPhase == FlyingMeleeAttackPhase.Preparing ||
                _visualPhase == FlyingMeleeAttackPhase.Striking) && Mathf.Abs(movement.y) <= DirectionTolerance;
            allowedDistance = _airSensor.CalculateAllowedTravelDistance(movement / distance, distance, CollisionSkin, isHorizontalAttack);
        }
        FaceDirection(facingDirection);
        _rigidbody.MovePosition(_rigidbody.position + movement.normalized * allowedDistance);
        return allowedDistance >= distance;
    }

    public void FaceDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= DirectionTolerance)
        {
            return;
        }
        Vector3 scale = transform.localScale;
        if (Mathf.Abs(direction.x) > HorizontalDirectionTolerance)
        {
            scale.x = Mathf.Abs(scale.x) * Mathf.Sign(direction.x);
        }
        scale.y = Mathf.Abs(scale.y);
        transform.localScale = scale;
        transform.rotation = Quaternion.identity;
        if (_visualPhase == FlyingMeleeAttackPhase.Completed || _visualPhase == FlyingMeleeAttackPhase.Hovering ||
            _visualPhase == FlyingMeleeAttackPhase.Positioning)
        {
            return;
        }
        float tilt = _visualPhase == FlyingMeleeAttackPhase.Striking
            ? Mathf.Clamp(Mathf.Atan2(direction.y, Mathf.Abs(direction.x)) * Mathf.Rad2Deg, -_maxVisualTilt, _maxVisualTilt)
            : 0f;
        UpdateVisualTilt(tilt);
    }

    public void ResetVisualTilt()
    {
        UpdateVisualTilt(0f);
    }

    public void StopAttackPresentation()
    {
        _visualPhase = FlyingMeleeAttackPhase.Completed;
        _smoothedFlightVelocity = Vector2.zero;
        _flightTiltVelocity = 0f;
        if (_visualRoot != null)
        {
            _visualRoot.localRotation = Quaternion.identity;
        }
    }

    private void UpdateVisualTilt(float tilt)
    {
        if (_visualRoot != null)
        {
            float angle = Mathf.MoveTowardsAngle(_visualRoot.localEulerAngles.z, tilt, VisualTurnSpeed * Time.deltaTime);
            _visualRoot.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    public void PlayPhase(FlyingMeleeAttackPhase phase)
    {
        _visualPhase = phase;
        // 표현 연결이 잘못되어도 공격 타이밍은 바뀌지 않는다.
        int hash = phase switch
        {
            FlyingMeleeAttackPhase.Preparing => _prepareHash,
            FlyingMeleeAttackPhase.Striking => _strikeHash,
            FlyingMeleeAttackPhase.Recovering => _recoverHash,
            _ => _idleHash,
        };
        if (_animator == null || !_animator.HasState(AnimationLayer, hash))
        {
            Debug.LogWarning("FlyingMeleeAttackController: 표현 Animator 또는 상태 이름을 확인하세요.", this);
            return;
        }
        if (hash == _idleHash)
        {
            AnimatorStateInfo current = _animator.GetCurrentAnimatorStateInfo(AnimationLayer);
            // 비행 상태 사이에는 날갯짓을 이어 간다. 피격 모션도 중간에 덮어쓰지 않는다.
            if (current.fullPathHash == hash || current.tagHash == HitTagHash)
            {
                return;
            }
            if (_animator.IsInTransition(AnimationLayer))
            {
                AnimatorStateInfo next = _animator.GetNextAnimatorStateInfo(AnimationLayer);
                if (next.fullPathHash == hash || next.tagHash == HitTagHash)
                {
                    return;
                }
            }
        }
        _animator.Play(hash, AnimationLayer, 0f);
    }

    public void PlayFlightAnimation()
    {
        PlayPhase(FlyingMeleeAttackPhase.Completed);
    }

    public void DisableContactDamage()
    {
        if (_contactDamage != null && !_isContactDamageSuppressed)
        {
            _wasContactDamageEnabled = _contactDamage.enabled;
            _isContactDamageSuppressed = true;
            _contactDamage.enabled = false;
        }
    }

    public void RestoreContactDamage()
    {
        if (_contactDamage != null && _isContactDamageSuppressed)
        {
            _contactDamage.enabled = _wasContactDamageEnabled;
            _isContactDamageSuppressed = false;
        }
    }

    private void OnDisable()
    {
        if (_enemy != null && _enemy.StateMachine != null && _enemy.StateMachine.CurrentState is FlyingMeleeAttackState attackState)
        {
            attackState.Exit();
        }
        else if (_combat != null)
        {
            _combat.StopAttack();
            RestoreContactDamage();
        }
        StopAttackPresentation();
    }
}
