using UnityEngine;

public class KnockbackState : EnemyState
{
    private const int FirstComboStep = 1;
    private const int SecondComboStep = 2;
    private const int ThirdComboStep = 3;

    private readonly Enemy _enemy;
    private readonly HitData _hitData;
    private readonly bool _isHorizontalOnly;

    private float _minKnockbackTimer = 0f;
    private const float MinKnockbackDuration = 0.8f;

    public KnockbackState(Enemy enemy, HitData hitData)
    {
        _enemy = enemy;
        _hitData = hitData;
        _isHorizontalOnly = hitData.IsTalisman || hitData.ComboStep == FirstComboStep || hitData.ComboStep == SecondComboStep;
    }

    public override void Enter()
    {
        Debug.Log("적 넉백!");
        _enemy.MoveType.ResetLightKnockback();
        _enemy.Combat.StopAttack();
        _enemy.MoveType.StopMove();
        // AddForce(Impulse)는 질량에 따라 튀는 높이가 달라지고 연타 시 속도가 누적되므로 직접 대입
        float verticalVelocity = _hitData.IsTalisman
            ? _enemy.EnemyRb.linearVelocity.y
            : _isHorizontalOnly ? 0f : _hitData.KnockbackForce;
        Vector2 knockbackVelocity = Vector2.up * verticalVelocity;
        knockbackVelocity.x = Mathf.Sign(_hitData.AttackDirection.x) * _hitData.KnockbackForce;

        _enemy.EnemyRb.linearVelocity = _enemy.MoveType.CalculateSafeKnockbackVelocity(knockbackVelocity);
        _minKnockbackTimer = _hitData.IsTalisman ? 0f : MinKnockbackDuration;
    }

    public override void FixedUpdate()
    {
        _enemy.MoveType.KnockbackPhysicsUpdate(_isHorizontalOnly);
    }

    public override void Update()
    {
        if (_minKnockbackTimer > 0f)
        {
            _minKnockbackTimer -= Time.deltaTime;
            return;
        }

        // 종료 판정은 이동 타입과 넉백 방향에 따라 구분
        if (!_enemy.MoveType.IsKnockbackFinished(_isHorizontalOnly)) return;

        // 짧은 밀림이 끝나도 피격 모션을 새 공격으로 끊지 않는다.
        bool shouldStartSpacing = ShouldStartFlyingRangedSpacing();
        bool shouldRecoverHit = _hitData.IsTalisman || shouldStartSpacing;
        if (shouldRecoverHit && _enemy.Animation.IsPlayingHit())
        {
            return;
        }

        _enemy.MoveType.StopMove();
        if (_enemy.Type == EnemyType.Buffer)
        {
            _enemy.StateMachine.ChangeState(new FindEnemyState(_enemy));
            return;
        }

        if (shouldStartSpacing)
        {
            _enemy.StateMachine.ChangeState(new FlyingRangedSpacingState(_enemy));
        }
        else
        {
            _enemy.StateMachine.ChangeState(new ChaseState(_enemy));
        }
    }

    private bool ShouldStartFlyingRangedSpacing()
    {
        return _hitData.ComboStep == ThirdComboStep && !_hitData.IsTalisman && !_hitData.IsReflectedProjectile &&
            _enemy.Combat is EnemyRangedCombat && _enemy.MoveType is EnemyFlyingMove;
    }

    public override void Exit()
    {
        Debug.Log("적 넉백 종료!");
    }
}
