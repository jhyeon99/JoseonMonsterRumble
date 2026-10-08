using UnityEngine;

public abstract class EnemyMoveType : MonoBehaviour
{
    protected Enemy Enemy { get; private set; }
    protected Rigidbody2D Rigidbody { get; private set; }
    [HideInInspector] public bool IsBackMove = false;

    [Header("넉백")]

    [Tooltip("넉백 속도를 초당 얼마나 줄일지. 속도가 거의 0이 되면 넉백 종료")]
    [SerializeField, Min(0.01f)]
    private float _knockbackDeceleration = 20f;

    private const float KnockbackStopSpeed = 0.1f;

    protected virtual void Awake()
    {
        Enemy = GetComponent<Enemy>();
        Rigidbody = GetComponent<Rigidbody2D>();
    }

    public abstract void Move(Vector2 direction);
    public abstract void BackMove(Vector2 direction);
    public abstract void StopMove();

    public virtual void ChaseMove(Enemy target)
    {
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

    public bool IsArrivedAtStart()
    {
        Vector2 startPos = Enemy.MoveArea.StartPosition;
        return Vector2.Distance(Rigidbody.position, startPos) < 0.1f;
    }
}
