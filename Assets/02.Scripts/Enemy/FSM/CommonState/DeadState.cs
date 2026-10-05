using UnityEngine;

public class DeadState : EnemyState
{
    private Enemy _enemy;
    private float _dieTimer;

    public DeadState(Enemy enemy)
    {
        _enemy = enemy;
    }

    public override void Enter()
    {
        Debug.Log("적 사망!");
        _enemy.Animation.PlayDie();
        DisableRigidbody();
        _dieTimer = _enemy.Data.DieTime;
    }

    public override void Update()
    {
        _dieTimer -= Time.deltaTime;
        if (_dieTimer <= 0)
        {
            SoulItem soulItem = ItemPool.Instance.GetSoulItem();
            soulItem.transform.position = _enemy.GetSoulSpawnPosition();
            _enemy.gameObject.SetActive(false);
        }
    }

    private void DisableRigidbody()
    {
        _enemy.EnemyRb.linearVelocity = Vector2.zero;
        _enemy.EnemyRb.angularVelocity = 0f;
        _enemy.EnemyRb.simulated = false;
    }
}