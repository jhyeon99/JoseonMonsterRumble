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
        _targetAttacker = EnemyManager.Instance.SelectRandomAttacker();

        if (_targetAttacker == null)
        {
            StateMachine.ChangeState(new EnragedState(this));
            return;
        }

        _targetAttacker.Health.OnDeath += HandleTargetDeath;
        StateMachine.ChangeState(new ChaseEnemyState(this, _targetAttacker.transform));
    }

    private void HandleAllAttackersKilled()
    {
        StateMachine.ChangeState(new EnragedState(this));
    }

    private void HandleTargetDeath()
    {
        UnsubscribeTarget();
    }

    private void UnsubscribeTarget()
    {
        if (_targetAttacker != null)
        {
            _targetAttacker.Health.OnDeath -= HandleTargetDeath;
            _targetAttacker = null;
            StateMachine.ChangeState(new FindEnemyState(this));
        }
    }
}
