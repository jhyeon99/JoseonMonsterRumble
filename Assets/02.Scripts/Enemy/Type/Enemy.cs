using System;
using TMPro;
using UnityEngine;

public abstract class Enemy : MonoBehaviour, IDamageable
{
    [SerializeField] private EnemyData _data;
    public EnemyData Data => _data;

    [SerializeField] private Transform _target;

    protected float CurrentHp { get; private set; }
    private EnemyStateMachine _stateMachine;
    private EnemyCombat _combat;
    private EnemyAnimation _animation;
    private EnemyMoveType _moveType;
    private float _attackTime;

    public EnemyStateMachine StateMachine => _stateMachine;
    public EnemyCombat Combat => _combat;
    public EnemyAnimation Animation => _animation;
    public EnemyMoveType MoveType => _moveType;
    public float AttackTime => _attackTime;

    protected virtual void Awake()
    {
        _attackTime = _data.AttackDurationTime + _data.AttackRecoveryTime;
        _stateMachine = new EnemyStateMachine();
        _combat = GetComponent<EnemyCombat>();
        _animation = GetComponent<EnemyAnimation>();
        _moveType = GetComponent<EnemyMoveType>();
        CurrentHp = _data.MaxHp;
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

    public bool TakeDamage(HitData hitData)
    {
        if (CurrentHp <= 0f) return false;

        CurrentHp -= hitData.Damage;
        Debug.Log($"현재 체력 {CurrentHp}");

        if (CurrentHp <= 0f)
        {
            CurrentHp = 0f;
            StateMachine.ChangeState(new DeadState(this));
            return true;
        }

        // StateMachine.ChangeState(new HitState(this));
        return true;
    }
}