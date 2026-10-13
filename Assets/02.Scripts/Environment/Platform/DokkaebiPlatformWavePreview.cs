using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>도깨비 위에서 실제 전투와 두 번 이상의 분노를 확인하는 3웨이브 프리뷰.</summary>
[DisallowMultipleComponent]
public sealed class DokkaebiPlatformWavePreview : MonoBehaviour
{
    [System.Serializable]
    private sealed class SpawnEntry
    {
        [SerializeField] private GameObject _prefab;
        [SerializeField] private Vector2 _surfaceOffset;
        public GameObject Prefab => _prefab;
        public Vector2 SurfaceOffset => _surfaceOffset;
    }

    [System.Serializable]
    private sealed class Wave
    {
        [SerializeField] private SpawnEntry[] _enemies;
        [SerializeField, Min(0)] private int _minimumRageCount;
        public SpawnEntry[] Enemies => _enemies;
        public int MinimumRageCount => _minimumRageCount;
    }

    [SerializeField] private DokkaebiPlatformRage _rage;
    [SerializeField] private Collider2D _platformSurface;
    [SerializeField] private Wave[] _waves;
    [SerializeField] private GameObject _reserveEnemyPrefab;
    [SerializeField, Min(0.1f)] private float _spawnInterval = 0.8f;
    [SerializeField, Min(0f)] private float _waveInterval = 2f;
    [Header("임시 추락 테스트")]
    [SerializeField] private bool _testFallOnStart;
    [Header("클리어 후 스턴과 추락")]
    [SerializeField, Min(0f)] private float _stunPause = 0.6f;
    [SerializeField] private float _fallTiltAngle = -25f;
    [SerializeField, Min(0.01f)] private float _tiltDuration = 0.7f;
    [SerializeField] private float _fallHorizontalSpeed = 1f;
    [SerializeField, Min(0.1f)] private float _fallGravity = 18f;
    [SerializeField, Min(1f)] private float _fallDistance = 20f;
    [SerializeField, ReadOnly] private int _currentWave;
    [SerializeField, ReadOnly] private int _aliveEnemyCount;
    [SerializeField, ReadOnly] private bool _isCompleted;
    [SerializeField, ReadOnly] private bool _isFalling;

    private readonly List<Enemy> _spawnedEnemies = new List<Enemy>();
    private Collider2D _playerBody;
    private PlayerHealth _playerHealth;
    private int _rageCountAtStart;
    private bool _spawnReserveOnRight;

    public bool IsCompleted => _isCompleted;
    public bool IsFalling => _isFalling;

    private IEnumerator Start()
    {
        // 씬 적 활성화 관리자가 초기 적을 캐시한 뒤 동적 웨이브를 생성한다.
        yield return null;
        if (_testFallOnStart)
        {
            StartImmediateFallTest();
            yield break;
        }
        if (_rage == null || !_rage.isActiveAndEnabled || _platformSurface == null || _waves == null || _waves.Length != 3 ||
            !IsEnemyPrefabValid(_reserveEnemyPrefab))
        {
            Debug.LogWarning("DokkaebiPlatformWavePreview: 발판, 분노, 3웨이브 또는 보충 적 연결이 없습니다.", this);
            yield break;
        }
        foreach (Wave wave in _waves)
        {
            if (wave == null || wave.Enemies == null || wave.Enemies.Length == 0)
            {
                Debug.LogWarning("DokkaebiPlatformWavePreview: 비어 있는 웨이브가 있습니다.", this);
                yield break;
            }
            foreach (SpawnEntry entry in wave.Enemies)
            {
                if (entry == null || !IsEnemyPrefabValid(entry.Prefab))
                {
                    Debug.LogWarning("DokkaebiPlatformWavePreview: 웨이브 적 Prefab 연결이 없습니다.", this);
                    yield break;
                }
            }
        }
        var player = GameObject.FindWithTag(Constants.Tags.Player);
        if (player != null)
        {
            _playerBody = player.GetComponent<Collider2D>();
            _playerHealth = player.GetComponent<PlayerHealth>();
        }
        if (_playerBody == null || _playerHealth == null)
        {
            Debug.LogWarning("DokkaebiPlatformWavePreview: 플레이어 Collider 또는 PlayerHealth가 없습니다.", this);
            yield break;
        }
        StartCoroutine(RunWaves());
    }

    private static bool IsEnemyPrefabValid(GameObject prefab)
    {
        if (prefab == null || !prefab.TryGetComponent(out Enemy enemy)) return false;
        return enemy.Data != null && prefab.GetComponent<BoxCollider2D>() != null;
    }

    private IEnumerator RunWaves()
    {
        var pollWait = new WaitForSeconds(0.2f);
        while (!_playerBody.IsTouching(_platformSurface))
        {
            if (_playerHealth.IsDead) yield break;
            yield return pollWait;
        }
        _rageCountAtStart = _rage.RageActivationCount;
        for (int index = 0; index < _waves.Length; index++)
        {
            Wave wave = _waves[index];
            _currentWave = index + 1;
            Debug.Log($"도깨비 발판: {_currentWave}/3 웨이브 시작", this);
            foreach (SpawnEntry entry in wave.Enemies)
            {
                if (_playerHealth.IsDead) { DestroySpawnedEnemies(); yield break; }
                SpawnEnemy(entry.Prefab, entry.SurfaceOffset);
                yield return new WaitForSeconds(_spawnInterval);
            }
            while (true)
            {
                if (_playerHealth.IsDead) { DestroySpawnedEnemies(); yield break; }
                UpdateAliveEnemyCount();
                bool hasRequiredRage = _rage.RageActivationCount - _rageCountAtStart >= wave.MinimumRageCount;
                bool isFinalWave = index == _waves.Length - 1;
                if (_aliveEnemyCount == 0 && hasRequiredRage && (isFinalWave || !_rage.IsRaging)) break;
                // 원샷·마무리 공격으로 적이 먼저 소진돼도 실제 적중 기회를 계속 제공한다.
                if (_aliveEnemyCount == 0 && !hasRequiredRage && !_rage.IsRaging &&
                    _playerBody.IsTouching(_platformSurface))
                {
                    _spawnReserveOnRight = !_spawnReserveOnRight;
                    SpawnEnemy(_reserveEnemyPrefab, new Vector2(_spawnReserveOnRight ? 3f : -3f, 0f));
                }
                yield return pollWait;
            }
            if (index < _waves.Length - 1) yield return new WaitForSeconds(_waveInterval);
        }
        if (!_rage.TryStartStun()) yield break;
        _isCompleted = true;
        Debug.Log("도깨비 발판: 3웨이브 방어 완료", this);
        yield return FallAfterStun(_stunPause);
    }

    [ContextMenu("테스트/즉시 스턴 추락")]
    public void StartImmediateFallTest()
    {
        if (!Application.isPlaying || _isFalling) return;
        if (_rage == null || _platformSurface == null)
        {
            Debug.LogWarning("DokkaebiPlatformWavePreview: 추락 테스트의 분노 또는 발판 연결이 없습니다.", this);
            return;
        }
        if (!_rage.IsStunned && !_rage.TryStartStun()) return;
        StopAllCoroutines();
        DestroySpawnedEnemies();
        StartCoroutine(FallAfterStun(0f));
    }

    private IEnumerator FallAfterStun(float stunPause)
    {
        if (stunPause > 0f) yield return new WaitForSeconds(stunPause);
        _isFalling = true;
        _platformSurface.enabled = false;
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        float elapsed = 0f;
        float fallDistance = 0f;
        while (fallDistance < _fallDistance)
        {
            elapsed += Time.deltaTime;
            fallDistance = 0.5f * _fallGravity * elapsed * elapsed;
            transform.position = startPosition + new Vector3(_fallHorizontalSpeed * elapsed, -fallDistance, 0f);
            float tiltProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / _tiltDuration));
            transform.rotation = startRotation * Quaternion.AngleAxis(_fallTiltAngle * tiltProgress, Vector3.forward);
            yield return null;
        }
        _isFalling = false;
        gameObject.SetActive(false);
    }

    private void SpawnEnemy(GameObject prefab, Vector2 surfaceOffset)
    {
        Bounds surface = _platformSurface.bounds;
        BoxCollider2D body = prefab.GetComponent<BoxCollider2D>();
        Vector3 scale = prefab.transform.localScale;
        var position = new Vector3(
            surface.center.x + surfaceOffset.x - body.offset.x * scale.x,
            surface.max.y + surfaceOffset.y + 0.08f - (body.offset.y - body.size.y * 0.5f) * Mathf.Abs(scale.y),
            transform.position.z);
        GameObject instance = Instantiate(prefab, position, Quaternion.identity, transform);
        Enemy enemy = instance.GetComponent<Enemy>();
        // 지상 적만 발판 가장자리 안에 제한한다. 비행 적은 공격·우회에 필요한 원본 범위를 유지한다.
        if (enemy.MoveArea != null && enemy.MoveType is EnemyGroundMove)
        {
            enemy.MoveArea.CenterOffset = new Vector2(surface.center.x - position.x, surface.max.y + 2f - position.y);
            enemy.MoveArea.AreaSize = new Vector2(Mathf.Max(1f, surface.size.x - 3f), 6f);
        }
        _spawnedEnemies.Add(enemy);
        _aliveEnemyCount++;
    }

    private void UpdateAliveEnemyCount()
    {
        _aliveEnemyCount = 0;
        for (int index = _spawnedEnemies.Count - 1; index >= 0; index--)
        {
            Enemy enemy = _spawnedEnemies[index];
            if (enemy == null || enemy.IsDefeated || (enemy.Health != null && !enemy.Health.IsAlive))
            {
                _spawnedEnemies.RemoveAt(index);
                continue;
            }
            if (enemy.transform.position.y < _platformSurface.bounds.max.y - 3f)
            {
                Destroy(enemy.gameObject);
                _spawnedEnemies.RemoveAt(index);
                continue;
            }
            _aliveEnemyCount++;
        }
    }

    private void DestroySpawnedEnemies()
    {
        foreach (Enemy enemy in _spawnedEnemies)
        {
            if (enemy != null) Destroy(enemy.gameObject);
        }
        _spawnedEnemies.Clear();
        _aliveEnemyCount = 0;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        DestroySpawnedEnemies();
    }
}
