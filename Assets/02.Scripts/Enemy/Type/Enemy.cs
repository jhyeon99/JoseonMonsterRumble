using System;
using TMPro;
using UnityEngine;

public abstract class Enemy : MonoBehaviour
{
    [SerializeField] private EnemyData _data;
    public EnemyData Data => _data;

    private Transform _target;
    private EnemyStateMachine _stateMachine;
    private EnemyCombat _combat;
    private EnemyAnimation _animation;
    private EnemyMoveType _moveType;
    private IAttackDirectionLock _directionLock;

    public EnemyStateMachine StateMachine => _stateMachine;
    public EnemyCombat Combat => _combat;
    public EnemyAnimation Animation => _animation;
    public EnemyMoveType MoveType => _moveType;
    public IAttackDirectionLock DirectionLock => _directionLock;

    protected virtual void Awake()
    {
        _target = FindFirstObjectByType<Player>().transform;
        _stateMachine = new EnemyStateMachine();
        _combat = GetComponent<EnemyCombat>();
        _animation = GetComponent<EnemyAnimation>();
        _moveType = GetComponent<EnemyMoveType>();
        _directionLock = GetComponent<IAttackDirectionLock>();
    }

    protected virtual void Start()
    {
        _stateMachine.ChangeState(new IdleState(this));
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
