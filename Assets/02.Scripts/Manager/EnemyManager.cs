using UnityEngine;
using System;
using System.Collections.Generic;

public class EnemyManager : MonoBehaviour
{
    private static EnemyManager _instance = null;
    public static EnemyManager Instance => _instance;
    public List<Enemy> ActiveEnemies { get; private set; } = new List<Enemy>();

    public event Action OnAllAttackersKilled;

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
            CheckAttackersLive();
        }
    }

    [ContextMenu("Test Select Random Attacker")]
    public Enemy SelectRandomAttacker()
    {
        List<Enemy> attackers = ActiveEnemies.FindAll(CheckAttackerType);

        if (attackers.Count == 0) return null;

        int randomIndex = UnityEngine.Random.Range(0, attackers.Count);
        return attackers[randomIndex];
    }

    private void CheckAttackersLive()
    {
        bool hasAttacker = ActiveEnemies.Exists(CheckAttackerType);

        if (!hasAttacker)
        {
            OnAllAttackersKilled?.Invoke();
        }
    }

    private bool CheckAttackerType(Enemy enemy)
    {
        return enemy.Type == EnemyType.Attacker;
    }
}
