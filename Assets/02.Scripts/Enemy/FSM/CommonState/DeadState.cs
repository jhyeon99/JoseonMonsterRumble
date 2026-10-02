using UnityEngine;

public class DeadState : EnemyState
{
    private Enemy _enemy;

    public DeadState(Enemy enemy)
    {
        _enemy = enemy;
    }

    public override void Enter()
    {
        Debug.Log("적 사망!");
        _enemy.gameObject.SetActive(false);
        SoulItem soulItem = ItemPool.Instance.GetSoulItem();
        soulItem.transform.position = _enemy.transform.position;
    }
}
