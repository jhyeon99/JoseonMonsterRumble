using UnityEngine;

public class DeadState : EnemyState
{
    private readonly Enemy _enemy;
    private readonly EnemyFlyingMove _fallingMove;
    private float _dieTimer;

    public DeadState(Enemy enemy)
    {
        _enemy = enemy;
        if (enemy.Combat is EnemyMeleeCombat melee && melee.FlyingAttack != null)
        {
            _fallingMove = enemy.MoveType as EnemyFlyingMove;
        }
    }

    public override void Enter()
    {
        _enemy.DirectionLock.LockDirection();
        Debug.Log("적 사망!");
        _enemy.Animation.PlayDie();
        if (_fallingMove != null)
        {
            _enemy.Combat.StopAttack();
            _fallingMove.StartDeathFall();
        }
        else
        {
            DisableRigidbody();
        }
        _dieTimer = _fallingMove == null ? _enemy.Data.DieTime : 0f;

        if (ItemPool.Instance != null)
        {
            ItemPool.Instance.SpawnDeathSouls(_enemy.GetEnemyCenterPosition(), 5);
        }
    }

    public override void Update()
    {
        if (_fallingMove != null)
        {
            if (_enemy.Animation.IsDeathAnimationComplete())
            {
                _enemy.gameObject.SetActive(false);
            }
            return;
        }
        _dieTimer -= Time.deltaTime;
        if (_dieTimer <= 0)
        {
            _enemy.gameObject.SetActive(false);
        }
    }

    public override void FixedUpdate()
    {
        if (_fallingMove != null)
        {
            _fallingMove.UpdateDeathFall();
        }
    }

    private void DisableRigidbody()
    {
        _enemy.EnemyRb.linearVelocity = Vector2.zero;
        _enemy.EnemyRb.angularVelocity = 0f;
        _enemy.EnemyRb.simulated = false;
    }
}
