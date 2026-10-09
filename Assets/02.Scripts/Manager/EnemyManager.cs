using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;

public class EnemyManager : MonoBehaviour
{
    private static EnemyManager _instance;
    public static EnemyManager Instance => _instance;
    public List<Enemy> ActiveEnemies { get; private set; } = new List<Enemy>();
    public event Action OnAllAttackersKilled;
    private readonly HashSet<Enemy> _allEnemies = new HashSet<Enemy>();

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
        CacheSceneEnemies();
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
    }

    private void OnDestroy()
    {
        if (_instance != this) return;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        _instance = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CacheSceneEnemies();
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        _allEnemies.RemoveWhere(enemy => enemy == null);
        ActiveEnemies.RemoveAll(enemy => enemy == null);
    }

    private void CacheSceneEnemies()
    {
        // 최초 초기화와 씬 로드 때만 Awake가 실행되지 않은 비활성 적까지 등록한다.
        _allEnemies.RemoveWhere(enemy => enemy == null);
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Enemy enemy in enemies) RegisterEnemy(enemy);
    }

    public void RegisterEnemy(Enemy enemy)
    {
        if (enemy != null) _allEnemies.Add(enemy);
    }

    public void UnregisterEnemy(Enemy enemy)
    {
        _allEnemies.Remove(enemy);
        ActiveEnemies.Remove(enemy);
    }

    public void AddEnemy(Enemy enemy)
    {
        if (enemy == null) return;
        RegisterEnemy(enemy);
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
        // 비활성화된 적도 유지하는 캐시를 순회하며, 조회 시 배열을 생성하지 않는다.
        foreach (Enemy enemy in _allEnemies)
        {
            if (enemy != null && enemy.Type == EnemyType.Attacker && !enemy.IsDefeated &&
                (enemy.Health == null || enemy.Health.IsAlive)) return true;
        }
        return false;
    }
}
