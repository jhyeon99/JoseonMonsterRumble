using System;
using TMPro;
using UnityEngine;

public enum EnemyType
{
    Attacker,
    Buffer
}

public abstract class Enemy : MonoBehaviour
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

    protected virtual void OnDisable()
    {
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
    }

    protected virtual void Start()
    {
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
        _stateMachine.FixedUpdate();
    }

    public float GetDistanceToPlayer()
    {
        return Vector2.Distance(transform.position, _target.position);
    }

    public Vector2 GetDirectionToPlayer()
    {
        return (_target.position - transform.position).normalized;
    }
}
