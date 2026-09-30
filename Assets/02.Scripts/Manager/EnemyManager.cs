using UnityEngine;
using System;
using System.Collections.Generic;

public class EnemyManager : MonoBehaviour
{
    private static EnemyManager _instance = null;
    public static EnemyManager Instance => _instance;
    public List<Enemy> ActiveEnemies { get; private set; } = new List<Enemy>();

    public event Action OnAttackersKilled;

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

    public void RemoveEnemy(Enemy enemy)
    {
        if (ActiveEnemies.Contains(enemy))
        {
            ActiveEnemies.Remove(enemy);
            CheckRemainingAttackers();
        }
    }

    private void CheckRemainingAttackers()
    {
        bool hasAttacker = ActiveEnemies.Exists(CheckAttackerType);

        if (!hasAttacker)
        {
            OnAttackersKilled?.Invoke();
        }
    }

    private bool CheckAttackerType(Enemy enemy)
    {
        return enemy.Type == EnemyType.Attacker;
    }
}