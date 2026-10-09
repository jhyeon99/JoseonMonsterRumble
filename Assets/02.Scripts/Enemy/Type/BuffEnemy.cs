using UnityEngine;

public class BuffEnemy : Enemy
{
    private Enemy _targetAttacker;
    public Enemy TargetAttacker => _targetAttacker;
    private bool _started;

    protected override void Start()
    {
        base.Start();
        _started = true;
        EnemyManager.Instance.OnAllAttackersKilled += HandleAllAttackersKilled;
        StateMachine.ChangeState(new FindEnemyState(this));
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (!_started || IsDefeated) return;
        if (EnemyManager.Instance != null)
            EnemyManager.Instance.OnAllAttackersKilled += HandleAllAttackersKilled;
        // 광폭화는 유지하고, 해제된 버프 대상은 다시 탐색한다.
        if (!(StateMachine.CurrentState is EnragedState))
            StateMachine.ChangeState(new FindEnemyState(this));
    }

    protected override void OnDisable()
    {
        if (EnemyManager.Instance != null)
            EnemyManager.Instance.OnAllAttackersKilled -= HandleAllAttackersKilled;
        // 비활성화 중에는 공격력 증가와 대상 예약을 남기지 않는다.
        if (_started && !IsDefeated && !(StateMachine.CurrentState is EnragedState))
            StateMachine.ChangeState(new FindEnemyState(this));
        UnsubscribeTarget();
        base.OnDisable();
    }

    protected override void Update()
    {
        if (!IsDefeated && _targetAttacker != null)
        {
            if (_targetAttacker.IsDefeated || !_targetAttacker.Health.IsAlive)
            {
                HandleTargetDeath();
            }
            else if (!_targetAttacker.isActiveAndEnabled ||
                     !EnemyManager.Instance.IsNearBuffCamera(_targetAttacker))
            {
                // 살아 있는 대상의 범위 이탈은 광폭화 조건이 아니다.
                StateMachine.ChangeState(new FindEnemyState(this));
                UnsubscribeTarget();
            }
        }
        base.Update();
    }

    [ContextMenu("Test FindNewTarget")]
    public override void FindNewTarget()
    {
        if (EnemyManager.Instance == null) return;
        if (!EnemyManager.Instance.HasAnyAttacker())
        {
            StateMachine.ChangeState(new EnragedState(this));
            return;
        }
        Enemy newTarget = EnemyManager.Instance.GetDamagedAttackerWithoutBuffer();
        if (newTarget != null)
        {
            UnsubscribeTarget();
            _targetAttacker = newTarget;
            _targetAttacker.IsTargetedForBuff = true;
            _targetAttacker.Health.OnDeath += HandleTargetDeath;
            StateMachine.ChangeState(new ChaseEnemyState(this, _targetAttacker));
        }
        else MoveType.StopMove();
    }

    private void HandleAllAttackersKilled()
    {
        if (!isActiveAndEnabled || IsDefeated) return;
        StateMachine.ChangeState(new EnragedState(this));
        UnsubscribeTarget();
    }

    private void HandleTargetDeath()
    {
        if (!isActiveAndEnabled || IsDefeated) return;
        // OnDeath는 대상이 DeadState/LaunchedState로 바뀌기 전에 호출된다.
        // 이벤트 자체를 사망 근거로 사용하며, 다른 아군 생존 여부와 무관하게 광폭화한다.
        if (!(StateMachine.CurrentState is EnragedState))
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
