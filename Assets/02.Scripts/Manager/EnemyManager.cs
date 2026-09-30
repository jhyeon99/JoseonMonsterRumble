using UnityEngine;
using System.Collections.Generic;

public class EnemyManager : MonoBehaviour
{
    private static EnemyManager _instance = null;
    public static EnemyManager Instance => _instance;
    public List<Enemy> ActiveEnemies { get; private set; } = new List<Enemy>();

    private void Awake()
    {
        if (_instance != null)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void AddEnemy(Enemy enemy)
    {
        if (!ActiveEnemies.Contains(enemy))
        {
            ActiveEnemies.Add(enemy);
        }
    }
}
