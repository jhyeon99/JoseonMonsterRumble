using UnityEngine;

public class EnemyRangedCombat : EnemyCombat
{
    private enum BurstPhase { None, WaitingForReplay, Replaying }

    [SerializeField] private Transform _muzzlePoint;
    [SerializeField] private ProjectileType _type;

    [Header("특수 발사 설정")]
    [SerializeField, Range(0f, 1f)] private float _burstChance = 0.15f;
    [SerializeField, Range(0f, 1f)] private float _spreadChance = 0.15f;
    [SerializeField, Min(0f)] private float _burstMinDistance = 6.5f;
    [SerializeField] private float _burstInterval = 0.5f;
    [SerializeField] private AnimationClip _burstAttackClip;
    [SerializeField, Min(0)] private int _burstResumeFrame = 32;
    [SerializeField] private float _spreadAngle = 15f;

    private BurstPhase _burstPhase;
    private float _burstReplayDelay;
    private float _burstResumeProgress;
    private float _burstFireProgress;
    private int _burstAttackStateHash;

    public bool IsBurstPending => _burstPhase != BurstPhase.None;

    public event System.Action OnTelegraphAttack;
    public event System.Action<Vector3> OnProjectileFired;
    private bool _spreadGroup, _spreadCueSent;

    public void OnTelegraph()
    {
        OnTelegraphAttack?.Invoke();
    }

    public override void Attack()
    {
        StopAttack();
        if (Enemy == null || Enemy.Target == null || _muzzlePoint == null)
        {
            return;
        }

        if (Enemy.MoveType is EnemyFlyingMove && (_burstChance > 0f || _spreadChance > 0f))
        {
            float roll = Random.value;
            float distanceToPlayer = Enemy.GetDistanceToPlayer();

            if (roll < _burstChance)
            {
                if (distanceToPlayer >= _burstMinDistance)
                {
                    StartBurst();
                    return;
                }
            }
            else if (_spreadChance > 0f && roll < _burstChance + _spreadChance)
            {
                FireSpreadProjectiles();
                return;
            }
        }

        FireSingleProjectile(0f);
    }

    private void FireSingleProjectile(float angleOffset = 0f)
    {
        if (Enemy == null || Enemy.Target == null || _muzzlePoint == null)
        {
            return;
        }

        Projectile projectile =
            ProjectilePool.Instance.GetProjectile(_type, _muzzlePoint, Enemy.Target, DamageIncreaseValue);
        if (projectile == null)
        {
            return;
        }

        projectile.transform.position = _muzzlePoint.position;
        projectile.transform.rotation = _muzzlePoint.rotation;

        Vector2 direction = Enemy.GetDirectionToPlayer();
        float baseCorrection = direction.x <= 0f ? 8.228f : -8.228f;
        projectile.transform.Rotate(0f, 0f, baseCorrection + angleOffset);
        if (!_spreadGroup || !_spreadCueSent)
        {
            _spreadCueSent = true;
            OnProjectileFired?.Invoke(_muzzlePoint.position);
        }
    }

    private void FireSpreadProjectiles()
    {
        _spreadGroup = true;
        _spreadCueSent = false;
        try
        {
            FireSingleProjectile(0f);
            FireSingleProjectile(_spreadAngle);
            FireSingleProjectile(-_spreadAngle);
        }
        finally { _spreadGroup = false; }
    }

    private void StartBurst()
    {
        FireSingleProjectile(0f);
        if (_burstAttackClip == null ||
            !Enemy.Animation.TryGetAttackPlayback(out AnimatorStateInfo state, out float playbackSpeed) ||
            playbackSpeed <= 0f)
        {
            Debug.LogError("EnemyRangedCombat: 버스트 공격 클립과 Attack 재생 상태를 확인하세요.", this);
            return;
        }

        _burstResumeProgress = _burstResumeFrame / (_burstAttackClip.frameRate * _burstAttackClip.length);
        _burstFireProgress = state.normalizedTime;
        if (_burstResumeProgress >= _burstFireProgress)
        {
            Debug.LogError("EnemyRangedCombat: 버스트 재시작 프레임은 발사 프레임보다 앞이어야 합니다.", this);
            StopAttack();
            return;
        }

        float replayDuration = (_burstFireProgress - _burstResumeProgress) * _burstAttackClip.length / playbackSpeed;
        _burstReplayDelay = Mathf.Max(0f, _burstInterval - replayDuration);
        _burstAttackStateHash = state.fullPathHash;
        _burstPhase = BurstPhase.WaitingForReplay;
    }

    private void Update()
    {
        if (!IsBurstPending)
        {
            return;
        }

        if (Enemy.IsDefeated || !(Enemy.StateMachine.CurrentState is AttackState) ||
            !Enemy.Animation.TryGetAttackPlayback(out AnimatorStateInfo state, out _) ||
            state.fullPathHash != _burstAttackStateHash)
        {
            StopAttack();
            return;
        }

        if (_burstPhase == BurstPhase.WaitingForReplay)
        {
            _burstReplayDelay -= Time.deltaTime;
            if (_burstReplayDelay <= 0f)
            {
                Enemy.Animation.ReplayAttackFrom(_burstAttackStateHash, _burstResumeProgress);
                _burstPhase = BurstPhase.Replaying;
            }

            return;
        }

        if (state.normalizedTime >= _burstFireProgress)
        {
            FireSingleProjectile(0f);
            StopAttack();
        }
    }

    public override void StopAttack()
    {
        _burstPhase = BurstPhase.None;
        _burstReplayDelay = 0f;
        _burstResumeProgress = 0f;
        _burstFireProgress = 0f;
        _burstAttackStateHash = 0;
    }

    private void OnDisable()
    {
        StopAttack();
    }
}
