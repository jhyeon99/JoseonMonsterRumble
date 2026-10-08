using UnityEngine;

public class AttackPrepareGroundState : EnemyState
{
    protected Enemy _enemy;
    protected float _attackPrepareTimer = 0;

    public AttackPrepareGroundState(Enemy enemy)
    {
        _enemy = enemy;
    }

    public override void Enter()
    {
        Debug.Log("적 공격 준비 시작");
        _attackPrepareTimer = _enemy.Data.AttackPrepareTime;
        _enemy.MoveType.StopMove();
        if (_enemy.Combat is EnemyMeleeCombat)
        {
            // 뒤로 물러나던 방향(플레이어 반대)을 보고 있을 수 있으므로, 방향을 잠그기 전에 플레이어 쪽을 보게 함
            // (휴식 → 추적 → 공격 준비가 한 프레임에 이어지면 Flip이 돌기 전에 잠겨서 뒤를 보고 공격하던 문제)
            FacePlayer();
            _enemy.Animation.PlayAttack();
        }
    }

    public override void FixedUpdate()
    {
        if (_enemy.Combat is EnemyRangedCombat)
        {
            if (_enemy.GetDistanceToPlayer() <= _enemy.Data.AttackDetectRange)
            {
                if (!_enemy.MoveType.IsEndPosition(_enemy.GetDirectionToPlayer()))
                {
                    _enemy.Animation.SetRunning(true);
                    _enemy.MoveType.BackMove(_enemy.GetDirectionToPlayer());
                }
                else
                {
                    _enemy.Animation.SetRunning(false);
                    _enemy.MoveType.StopMove();
                }
            }
            else
            {
                _enemy.Animation.SetRunning(false);
                _enemy.MoveType.StopMove();
            }
        }
    }

    public override void Update()
    {
        _attackPrepareTimer -= Time.deltaTime;
        if (_attackPrepareTimer <= 0)
        {
            Debug.Log("지상 적 공격 준비");
            _enemy.StateMachine.ChangeState(new AttackState(_enemy));
        }
    }

    private void FacePlayer()
    {
        float directionX = _enemy.GetDirectionToPlayer().x;
        if (Mathf.Abs(directionX) < 0.01f)
        {
            return;
        }

        Vector3 scale = _enemy.transform.localScale;
        scale.x = directionX > 0f ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
        _enemy.transform.localScale = scale;
    }

    public override void Exit()
    {
        _enemy.MoveType.IsBackMove = false;
        _enemy.MoveType.StopMove();
        Vector3 scale = _enemy.transform.localScale;
        if (_enemy.GetDirectionToPlayer().x > 0f)
        {
            scale.x = Mathf.Abs(scale.x);
        }
        else if (_enemy.GetDirectionToPlayer().x < 0f)
        {
            scale.x = -Mathf.Abs(scale.x);
        }

        _enemy.transform.localScale = scale;

        Debug.Log("적 공격 준비 완료");
    }
}
