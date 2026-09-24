using System;
using UnityEngine;

public abstract class Enemy : MonoBehaviour, IDamageable
{
    [SerializeField] private EnemyData _data;
    public EnemyData Data => _data;

    protected float CurrentHp { get; private set; }
    private EnemyStateMachine _stateMachine;
    private EnemyCombat _combat;
    private EnemyAnimation _animation;
    private EnemyMove _move;

    public EnemyStateMachine StateMachine => _stateMachine;
    public EnemyCombat Combat => _combat;
    public EnemyAnimation Animation => _animation;
    public EnemyMove Move => _move;

    protected virtual void Awake()
    {
        _stateMachine = new EnemyStateMachine();
        _combat = GetComponent<EnemyCombat>();
        _animation = GetComponent<EnemyAnimation>();
        _move = GetComponent<EnemyMove>();
        CurrentHp = _data.MaxHp;
    }

    protected virtual void Update()
    {
        StateMachine.Update();
    }

    protected void FixedUpdate()
    {
        StateMachine.FixedUpdate();
    }

    public bool TakeDamage(HitData hitData)
    {
        if (CurrentHp <= 0f) return false;

        CurrentHp -= hitData.Damage;

        if (CurrentHp <= 0f)
        {
            CurrentHp = 0f;
            // StateMachine.ChangeState(new DeadState(this));
            return true;
        }

        // StateMachine.ChangeState(new HitState(this));
        return true;
    }
}