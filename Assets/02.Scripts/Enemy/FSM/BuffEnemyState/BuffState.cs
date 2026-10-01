using UnityEngine;

public class BuffState : EnemyState
{
    private readonly Enemy _enemy;

    public BuffState(Enemy enemy)
    {
        _enemy = enemy;
    }
}
