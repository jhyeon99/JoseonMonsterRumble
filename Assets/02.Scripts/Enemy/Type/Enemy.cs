using System;
using TMPro;
using UnityEngine;

public enum EnemyType
{
    Attacker,
    Buffer
}

public abstract class Enemy : MonoBehaviour, IFinishable
{
    [SerializeField] private EnemyType _enemyType;
    public EnemyType Type => _enemyType;
    [SerializeField] private EnemyData _data;
    public EnemyData Data => _data;

    private Transform _target;
    private EnemyStateMachine _stateMachine;
    private EnemyCombat _combat;
    private EnemyAnimation _animation;
    private EnemyMoveType _moveType;
    private EnemyHealth _health;
    private IAttackDirectionLock _directionLock;
    private Rigidbody2D _enemyRb;
    private HitFlash _hitFlash;
    private EnemyMoveArea _moveArea;
    private Collider2D _enemyCollider;
    private float _radius;
    private float _rightAngle;
    private float _leftAngle;

    public Transform Target => _target;
    public EnemyStateMachine StateMachine => _stateMachine;
    public EnemyCombat Combat => _combat;
    public EnemyAnimation Animation => _animation;
    public EnemyMoveType MoveType => _moveType;
    public EnemyHealth Health => _health;
    public IAttackDirectionLock DirectionLock => _directionLock;
    public Rigidbody2D EnemyRb => _enemyRb;
    public HitFlash HitFlash => _hitFlash;
    public EnemyMoveArea MoveArea => _moveArea;
    public float Radius => _radius;
    public bool IsTargetedForBuff { get; set; } = false;
    public float RightAngle => _rightAngle;
    public float LeftAngle => _leftAngle;

    /// <summary>패링당해 그로기 중인지</summary>
    public bool IsGroggy => _stateMachine.CurrentState is GroggyState;

    /// <summary>죽는 중이거나 날아가는 중 (더 이상 행동 / 피격 / 공격하지 않음)</summary>
    public bool IsDefeated => _stateMachine?.CurrentState is DeadState || _stateMachine?.CurrentState is LaunchedState;

    /// <summary>그로기 시작 (패링당했을 때 공격 컴포넌트가 호출)</summary>
    public void EnterGroggy(float duration)
    {
        if (IsDefeated)
        {
            return;
        }

        _moveType.ResetLightKnockback();
        _stateMachine.ChangeState(new GroggyState(this, duration));
    }

    /// <summary>마무리 공격 준비 중 그로기가 풀리지 않게 고정. 그로기가 아니면 false</summary>
    public bool TryHoldGroggy()
    {
        if (_stateMachine.CurrentState is GroggyState groggy)
        {
            groggy.Hold();
            return true;
        }

        return false;
    }

    /// <summary>마무리 공격이 취소됐을 때 그로기 고정 해제</summary>
    public void ReleaseGroggy()
    {
        if (_stateMachine.CurrentState is GroggyState groggy)
        {
            groggy.Release();
        }
    }

    /// <summary>마무리 공격으로 날려 보내기 (사망 처리)</summary>
    public void Launch(LaunchSettings settings, float directionX)
    {
        _health.Launch(settings, directionX);
    }

    #region IFinishable (플레이어 마무리 공격)

    public bool CanBeFinished => isActiveAndEnabled && IsGroggy && !IsDefeated;
    public Transform FinisherTransform => transform;
    public bool TryHoldForFinisher() => TryHoldGroggy();
    public void ReleaseFinisherHold() => ReleaseGroggy();
    public void ReceiveFinisher(LaunchSettings launch, float directionX) => Launch(launch, directionX);

    #endregion

    protected virtual void OnEnable()
    {
        // 최초 등록은 Start에서 초기화가 끝난 뒤 수행한다.
        if (_stateMachine?.CurrentState != null && EnemyManager.Instance != null)
            EnemyManager.Instance.AddEnemy(this);
    }

    protected virtual void OnDisable()
    {
        if (EnemyManager.Instance != null)
            EnemyManager.Instance.RemoveEnemy(this);
    }

    protected virtual void Awake()
    {
        _stateMachine = new EnemyStateMachine();
        _combat = GetComponent<EnemyCombat>();
        _animation = GetComponent<EnemyAnimation>();
        _moveType = GetComponent<EnemyMoveType>();
        _health = GetComponent<EnemyHealth>();
        _directionLock = GetComponent<IAttackDirectionLock>();
        _enemyRb = GetComponent<Rigidbody2D>();
        _hitFlash = GetComponent<HitFlash>();
        _moveArea = GetComponent<EnemyMoveArea>();
        _enemyCollider = GetComponent<Collider2D>();
        _rightAngle = Data.AttackAngle;
        _leftAngle = 180f - Data.AttackAngle;
    }

    protected virtual void Start()
    {
        _radius = _enemyCollider.bounds.extents.y;
        EnemyManager.Instance.AddEnemy(this);
        _target = GameObject.FindWithTag(Constants.Tags.Player).transform;
    }

    public virtual void FindNewTarget()
    {
    }


    protected virtual void Update()
    {
        _stateMachine.Update();
    }

    protected void FixedUpdate()
    {
        if (IsDefeated || IsGroggy)
        {
            _moveType.ResetLightKnockback();
        }
        else if (_moveType.UpdateLightKnockbackPhysics())
        {
            return;
        }

        _stateMachine.FixedUpdate();
    }

    public float GetDistanceToPlayer()
    {
        return Vector2.Distance(transform.position, _target.position);
    }

    public float GetDistanceXToPlayer()
    {
        return Mathf.Abs(transform.position.x - _target.position.x);
    }

    public Vector2 GetDirectionToPlayer()
    {
        return (_target.position - transform.position).normalized;
    }

    public Vector2 GetEnemyCenterPosition()
    {
        return _enemyCollider.bounds.center;
    }
}
