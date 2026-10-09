using UnityEngine;

public abstract class EnemyMoveType : MonoBehaviour
{
    private enum LightKnockbackState { Idle, Moving }

    protected Enemy Enemy { get; private set; }
    protected Rigidbody2D Rigidbody { get; private set; }
    [HideInInspector] public bool IsBackMove = false;

    [Header("넉백")]

    [Tooltip("넉백 속도를 초당 얼마나 줄일지. 속도가 거의 0이 되면 넉백 종료")]
    [SerializeField, Min(0.01f)]
    private float _knockbackDeceleration = 20f;

    [Tooltip("1·2타의 실제 밀림 배율. 0이면 기존 넉백 처리를 사용")]
    [SerializeField, Min(0f)] private float _lightHitKnockbackMultiplier;

    private LightKnockbackState _lightKnockbackState;
    private float _lightKnockbackSpeed;
    private Vector2 _lightKnockbackStartPosition;

    private const float KnockbackStopSpeed = 0.1f;
    private const float SearchPositionTolerance = 0.1f;

    protected virtual void Awake()
    {
        Enemy = GetComponent<Enemy>();
        Rigidbody = GetComponent<Rigidbody2D>();
    }

    public abstract void Move(Vector2 direction);
    public abstract void BackMove(Vector2 direction);
    public abstract void StopMove();

    public virtual void RequestStopMove()
    {
        StopMove();
    }

    public virtual void UpdateWalkPhysics() { }

    public virtual bool TryMoveToSearchPosition(Vector2 position)
    {
        Vector2 offset = position - Rigidbody.position;
        if (offset.sqrMagnitude <= SearchPositionTolerance * SearchPositionTolerance)
        {
            return false;
        }
        Enemy.Animation.SetRunning(true);
        Rigidbody.linearVelocity = offset.normalized *
            Mathf.Min(Enemy.Data.MoveSpeed, offset.magnitude / Time.fixedDeltaTime);
        return true;
    }

    public bool TryStartLightKnockback(HitData hitData)
    {
        if (_lightHitKnockbackMultiplier <= 0f)
        {
            return false;
        }

        if (_lightKnockbackState == LightKnockbackState.Idle)
        {
            _lightKnockbackStartPosition = Rigidbody.position;
        }

        _lightKnockbackState = LightKnockbackState.Moving;
        _lightKnockbackSpeed = hitData.AttackDirection.x == 0f
            ? 0f
            : Mathf.Sign(hitData.AttackDirection.x) * hitData.KnockbackForce * _lightHitKnockbackMultiplier;
        return true;
    }

    // 일반 이동보다 먼저 처리하되 공격 상태와 애니메이션은 중단하지 않는다.
    public bool UpdateLightKnockbackPhysics()
    {
        if (_lightKnockbackState == LightKnockbackState.Idle)
        {
            return false;
        }

        if (Mathf.Abs(_lightKnockbackSpeed) <= KnockbackStopSpeed)
        {
            Vector2 stoppedVelocity = Rigidbody.linearVelocity;
            stoppedVelocity.x = 0f;
            Rigidbody.linearVelocity = stoppedVelocity;
            ResetLightKnockback();
            return false;
        }

        Vector2 velocity = Rigidbody.linearVelocity;
        velocity.x = _lightKnockbackSpeed;
        velocity = CalculateSafeKnockbackVelocity(velocity);
        Rigidbody.linearVelocity = velocity;
        _lightKnockbackSpeed = Mathf.MoveTowards(velocity.x, 0f,
            _knockbackDeceleration * Time.fixedDeltaTime);
        return true;
    }

    public void ResetLightKnockback()
    {
        if (_lightKnockbackState == LightKnockbackState.Moving)
        {
            HandleLightKnockbackEnd(Rigidbody.position - _lightKnockbackStartPosition);
        }

        _lightKnockbackState = LightKnockbackState.Idle;
        _lightKnockbackSpeed = 0f;
        _lightKnockbackStartPosition = Vector2.zero;
    }

    protected virtual void HandleLightKnockbackEnd(Vector2 displacement) { }

    public virtual Vector2 CalculateSafeKnockbackVelocity(Vector2 velocity)
    {
        return velocity;
    }

    protected void ResetLightKnockbackOrigin()
    {
        if (_lightKnockbackState == LightKnockbackState.Moving)
        {
            _lightKnockbackStartPosition = Rigidbody.position;
        }
    }

    protected virtual void OnDisable()
    {
        ResetLightKnockback();
    }

    public virtual void ChaseMove(Enemy target)
    {
    }

    public virtual bool IsEndPosition(Vector2 direction)
    {
        return false;
    }

    public virtual void MoveToStartPosition(Enemy target)
    {
    }

    public virtual void PatrolIdleMove()
    {
    }

    public virtual void MoveToInitialPosition()
    {
    }

    public virtual void StartQuadraticCharge(Vector2 playerPos)
    {
    }

    public virtual void QuadraticChargePhysicsUpdate()
    {
    }

    public virtual void StopQuadraticCharge()
    {
        StopMove();
    }

    /// <summary>
    /// 넉백 중 FixedUpdate마다 호출. 기본(공중/버프 적): 중력이 없으므로 속도를 직접 줄임
    /// </summary>
    public virtual void KnockbackPhysicsUpdate(bool isHorizontalOnly)
    {
        Rigidbody.linearVelocity = Vector2.MoveTowards(
            Rigidbody.linearVelocity,
            Vector2.zero,
            _knockbackDeceleration * Time.fixedDeltaTime
        );
    }

    /// <summary>
    /// 넉백이 끝났는지. 기본: 속도가 거의 0이 되면 종료.
    /// 넉백 직후에는 대입한 속도가 남아 있으므로 바로 끝나지 않음
    /// </summary>
    public virtual bool IsKnockbackFinished(bool isHorizontalOnly)
    {
        return Rigidbody.linearVelocity.sqrMagnitude <= KnockbackStopSpeed * KnockbackStopSpeed;
    }

    protected void UpdateHorizontalKnockback()
    {
        Vector2 velocity = Rigidbody.linearVelocity;
        velocity.x = Mathf.MoveTowards(velocity.x, 0f, _knockbackDeceleration * Time.fixedDeltaTime);
        Rigidbody.linearVelocity = velocity;
    }

    protected bool IsHorizontalKnockbackStopped()
    {
        return Mathf.Abs(Rigidbody.linearVelocity.x) <= KnockbackStopSpeed;
    }

    public virtual bool IsArrivedAtStart()
    {
        Vector2 startPos = Enemy.MoveArea.StartPosition;
        return Vector2.Distance(Rigidbody.position, startPos) < 0.1f;
    }
}
