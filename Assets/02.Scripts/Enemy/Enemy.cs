using System;
using UnityEngine;

public abstract class Enemy : MonoBehaviour, IDamageable
{
    [SerializeField] private EnemyData _data;
    public EnemyData Data => _data;

    protected float CurrentHp { get; private set; }
    protected EnemyStateMachine StateMachine { get; private set; }
    protected EnemyCombat Combat { get; private set; }
    protected EnemyAnimation Animation { get; private set; }
    protected EnemyMove Move { get; private set; }

    protected virtual void Awake()
    {
        StateMachine = new EnemyStateMachine();
        Combat = GetComponent<EnemyCombat>();
        Animation = GetComponent<EnemyAnimation>();
        Move = GetComponent<EnemyMove>();
        CurrentHp = _data.MaxHp;
    }

    protected virtual void Update()
    {
        StateMachine.UpdateState();
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
