using UnityEngine;
using System.Collections;

public class BuffEnemy : Enemy
{
    private Enemy _targetAttacker;
    public Enemy TargetAttacker => _targetAttacker;

    [Header("4색 버프 데이터")]
    [SerializeField] private SO_BuffTypeData _assignedBuffData;
    [SerializeField] private SO_BuffTypeData[] _availableBuffs;
    public SO_BuffTypeData CurrentBuffData { get; private set; }

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

        Enemy newTarget = EnemyManager.Instance.GetTargetAttackerForBuff();

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
