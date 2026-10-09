using UnityEngine;
using System;
using System.Collections.Generic;

public class EnemyManager : MonoBehaviour
{
    private static EnemyManager _instance;
    public static EnemyManager Instance => _instance;
    public List<Enemy> ActiveEnemies { get; private set; } = new List<Enemy>();
    public event Action OnAllAttackersKilled;

    [Header("버프 대상 카메라 범위")]
    [SerializeField] private Camera _buffCamera;
    [SerializeField] private Vector2 _buffCameraExtraRange = new Vector2(5f, 3f);

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

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    public void AddEnemy(Enemy enemy)
    {
        if (!ActiveEnemies.Contains(enemy)) ActiveEnemies.Add(enemy);
    }

    public void RemoveEnemy(Enemy enemy)
    {
        if (ActiveEnemies.Remove(enemy) && enemy.IsDefeated)
            CheckAttackersLive();
    }

    public Enemy GetDamagedAttackerWithoutBuffer()
    {
        Camera camera = _buffCamera != null ? _buffCamera : Camera.main;
        if (camera == null) return null;
        Enemy best = null;
        float bestHitTime = float.NegativeInfinity;
        float bestDistance = float.PositiveInfinity;
        foreach (Enemy enemy in ActiveEnemies)
        {
            if (enemy == null || !enemy.isActiveAndEnabled || enemy.IsDefeated ||
                enemy.Type != EnemyType.Attacker || enemy.Health == null ||
                !enemy.Health.IsDamaged || enemy.IsTargetedForBuff || !IsNearBuffCamera(enemy))
                continue;

            float hitTime = enemy.Health.LastDamageTime;
            float distance = ((Vector2)(enemy.transform.position - camera.transform.position)).sqrMagnitude;
            if (best == null || hitTime > bestHitTime || (hitTime == bestHitTime && distance < bestDistance))
            {
                best = enemy;
                bestHitTime = hitTime;
                bestDistance = distance;
            }
        }
        return best;
    }

    public bool IsNearBuffCamera(Enemy enemy)
    {
        Camera camera = _buffCamera != null ? _buffCamera : Camera.main;
        if (camera == null || enemy == null) return false;
        Vector3 position = enemy.transform.position;
        if (camera.orthographic)
        {
            Vector3 delta = position - camera.transform.position;
            return Mathf.Abs(delta.x) <= camera.orthographicSize * camera.aspect + _buffCameraExtraRange.x &&
                   Mathf.Abs(delta.y) <= camera.orthographicSize + _buffCameraExtraRange.y;
        }
        Vector3 viewport = camera.WorldToViewportPoint(position);
        return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
    }

    private void CheckAttackersLive()
    {
        if (!HasAnyAttacker()) OnAllAttackersKilled?.Invoke();
    }

    public bool HasAnyAttacker()
    {
        // 카메라 밖 비활성화된 생존자와 아직 Awake가 실행되지 않은 적도 포함한다.
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Enemy enemy in enemies)
        {
            if (enemy.Type == EnemyType.Attacker && !enemy.IsDefeated &&
                (enemy.Health == null || enemy.Health.IsAlive)) return true;
        }
        return false;
    }
}
