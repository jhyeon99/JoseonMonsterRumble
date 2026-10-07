using UnityEngine;
using System.Collections;

public class BuffEnemy : Enemy
{
    private Enemy _targetAttacker;
    public Enemy TargetAttacker => _targetAttacker;

    protected override void Start()
    {
        base.Start();
        EnemyManager.Instance.OnAllAttackersKilled += HandleAllAttackersKilled;
        StateMachine.ChangeState(new FindEnemyState(this));
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        EnemyManager.Instance.OnAllAttackersKilled -= HandleAllAttackersKilled;
        UnsubscribeTarget();
    }

    [ContextMenu("Test FindNewTarget")]
    public override void FindNewTarget()
    {
        if (!EnemyManager.Instance.HasAnyAttacker())
        {
            StateMachine.ChangeState(new EnragedState(this));
            return;
        }

        Enemy newTarget = EnemyManager.Instance.GetDamagedAttackerWithoutBuffer();

        if (newTarget != null)
        {
            _targetAttacker = newTarget;
            _targetAttacker.IsTargetedForBuff = true;
            _targetAttacker.Health.OnDeath += HandleTargetDeath;
            StateMachine.ChangeState(new ChaseEnemyState(this, _targetAttacker));
        }
        else
        {
            MoveType.StopMove();
        }
    }

    private void HandleAllAttackersKilled()
    {
        StateMachine.ChangeState(new EnragedState(this));
    }

    private void HandleTargetDeath()
    {
        StateMachine.ChangeState(new EnragedState(this));
        UnsubscribeTarget();
    }

    private void UnsubscribeTarget()
    {
        if (_targetAttacker != null)
        {
            _targetAttacker.IsTargetedForBuff = false;
            _targetAttacker.Health.OnDeath -= HandleTargetDeath;
            _targetAttacker = null;
        }
    }
}
