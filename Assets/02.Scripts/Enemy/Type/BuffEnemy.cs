using UnityEngine;

public class BuffEnemy : Enemy
{
    private Enemy _targetAttacker;
    public Enemy TargetAttacker => _targetAttacker;
    private bool _started;

    [Header("4색 버프 데이터")]
    [SerializeField] private SO_BuffTypeData _assignedBuffData;
    [SerializeField] private SO_BuffTypeData[] _availableBuffs;
    public SO_BuffTypeData CurrentBuffData { get; private set; }

    [Header("자폭")]
    [SerializeField, Min(0.1f)] private float _selfDestructDelay = 3f;
    public float SelfDestructDelay => _selfDestructDelay;

    private EnemyBuffVisualLink _visualLink;
    public EnemyBuffVisualLink VisualLink => _visualLink;

    protected override void Awake()
    {
        base.Awake();
        _visualLink = GetComponent<EnemyBuffVisualLink>();
        if (_visualLink == null)
        {
            _visualLink = gameObject.AddComponent<EnemyBuffVisualLink>();
        }

        InitializeBuffData();
    }

    private void InitializeBuffData()
    {
        if (_assignedBuffData != null)
        {
            CurrentBuffData = _assignedBuffData;
        }
        else
        {
            if (_availableBuffs == null || _availableBuffs.Length == 0)
            {
                _availableBuffs = Resources.LoadAll<SO_BuffTypeData>("Buffs");
            }

            if (_availableBuffs != null && _availableBuffs.Length > 0)
            {
                int randomIndex = Random.Range(0, _availableBuffs.Length);
                CurrentBuffData = _availableBuffs[randomIndex];
            }
        }

        if (CurrentBuffData != null && _visualLink != null)
        {
            _visualLink.ApplyBuffData(CurrentBuffData);
        }
    }

    protected override void Start()
    {
        base.Start();
        _started = true;
        if (EnemyManager.Instance != null)
        {
            EnemyManager.Instance.OnAllAttackersKilled += HandleAllAttackersKilled;
        }
        StateMachine.ChangeState(new FindEnemyState(this));
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (!_started || IsDefeated)
        {
            return;
        }
        if (EnemyManager.Instance != null)
        {
            EnemyManager.Instance.OnAllAttackersKilled += HandleAllAttackersKilled;
        }
        // 진행 중인 자폭은 유지하고, 해제된 버프 대상은 다시 탐색한다.
        if (!(StateMachine.CurrentState is SelfDestructState))
        {
            StateMachine.ChangeState(new FindEnemyState(this));
        }
    }

    protected override void OnDisable()
    {
        if (EnemyManager.Instance != null)
        {
            EnemyManager.Instance.OnAllAttackersKilled -= HandleAllAttackersKilled;
        }
        // 비활성화 중에는 공격력 증가와 대상 예약을 남기지 않는다.
        if (_started && !IsDefeated && !(StateMachine.CurrentState is SelfDestructState))
        {
            StateMachine.ChangeState(new FindEnemyState(this));
        }
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
            else if (!_targetAttacker.isActiveAndEnabled)
            {
                // 살아 있는 대상의 비활성화는 자폭 조건이 아니다.
                StateMachine.ChangeState(new FindEnemyState(this));
                UnsubscribeTarget();
            }
        }
        base.Update();
    }

    [ContextMenu("Test FindNewTarget")]
    public override void FindNewTarget()
    {
        if (EnemyManager.Instance == null || !EnemyManager.Instance.HasAnyAttacker())
        {
            StartSelfDestruct();
            return;
        }

        Enemy newTarget = EnemyManager.Instance.GetTargetAttackerForBuff(transform.position);

        if (newTarget != null)
        {
            UnsubscribeTarget();
            _targetAttacker = newTarget;
            _targetAttacker.IsTargetedForBuff = true;
            _targetAttacker.Health.OnDeath += HandleTargetDeath;
            StateMachine.ChangeState(new ChaseEnemyState(this, _targetAttacker));
        }
    }

    private void HandleAllAttackersKilled()
    {
        if (!isActiveAndEnabled || IsDefeated)
        {
            return;
        }
        StartSelfDestruct();
        UnsubscribeTarget();
    }

    private void HandleTargetDeath()
    {
        if (!isActiveAndEnabled || IsDefeated)
        {
            return;
        }
        StartSelfDestruct();
        UnsubscribeTarget();
    }

    private void StartSelfDestruct()
    {
        if (IsDefeated || StateMachine.CurrentState is SelfDestructState)
        {
            return;
        }

        StateMachine.ChangeState(new SelfDestructState(this));
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
