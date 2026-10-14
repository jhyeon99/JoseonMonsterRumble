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

        public SpawnEntry() { }
        public SpawnEntry(GameObject prefab, Vector2 surfaceOffset)
        {
            _prefab = prefab;
            _surfaceOffset = surfaceOffset;
        }
    }

    [System.Serializable]
    private sealed class SpawnGroup
    {
        [SerializeField] private SpawnEntry[] _enemies;
        public SpawnEntry[] Enemies => _enemies;

        public SpawnGroup() { }
        public SpawnGroup(SpawnEntry[] enemies)
        {
            _enemies = enemies;
        }
    }

    [System.Serializable]
    private sealed class Wave
    {
        [SerializeField] private SpawnGroup[] _groups;
        [SerializeField] private SpawnEntry[] _enemies;
        [SerializeField, Min(0)] private int _minimumRageCount;
        public SpawnGroup[] Groups => _groups;
        public SpawnEntry[] Enemies => _enemies;
        public int MinimumRageCount => _minimumRageCount;

        public IReadOnlyList<SpawnGroup> GetGroups()
        {
            if (_groups != null && _groups.Length > 0) return _groups;
            if (_enemies != null && _enemies.Length > 0) return new[] { new SpawnGroup(_enemies) };
            return System.Array.Empty<SpawnGroup>();
        }
    }

    [SerializeField] private DokkaebiPlatformRage _rage;
    [SerializeField] private Collider2D _platformSurface;
    [SerializeField] private Wave[] _waves;
    [SerializeField] private GameObject _reserveEnemyPrefab;
    [Header("이 웨이브의 환영 몬스터")]
    [SerializeField] private bool _useIllusionEnemies;
    [SerializeField] private Material _illusionMaterial;
    [Header("몬스터 등장 효과")]
    [SerializeField] private GameObject _spawnEffectPrefab;
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
    [Header("주변 환영 발판 소멸")]
    [SerializeField] private Transform _surroundingPlatformsRoot;
    [SerializeField] private GameObject[] _surroundingPlatforms;
    [SerializeField, Min(0.1f)] private float _platformDissolveDuration = 0.8f;
    [SerializeField, ReadOnly] private int _currentWave;
    [SerializeField, ReadOnly] private int _aliveEnemyCount;
    [SerializeField, ReadOnly] private bool _isCompleted;
    [SerializeField, ReadOnly] private bool _isFalling;

    private readonly List<Enemy> _spawnedEnemies = new List<Enemy>();
    private readonly List<GameObject> _spawnEffects = new List<GameObject>();
    private float _spawnEffectDuration;
    private Collider2D _playerBody;
    private PlayerHealth _playerHealth;
    private int _rageCountAtStart;
    private bool _spawnReserveOnRight;

    public bool IsCompleted => _isCompleted;
    public bool IsFalling => _isFalling;

    public static event System.Action OnDokkaebiPlatformCleared;
    public static event System.Action OnDokkaebiPlatformBattleStarted;

    private void OnDestroy()
    {
        OnDokkaebiPlatformCleared = null;
        OnDokkaebiPlatformBattleStarted = null;
    }

    private IEnumerator Start()
    {
        // 씬 적 활성화 관리자가 초기 적을 캐시한 뒤 동적 웨이브를 생성한다.
        yield return null;
        if (_testFallOnStart)
        {
            StartImmediateFallTest();
            yield break;
        }

        if (_rage == null || !_rage.isActiveAndEnabled || _platformSurface == null || _waves == null ||
            _waves.Length != 3 ||
            !IsEnemyPrefabValid(_reserveEnemyPrefab))
        {
            Debug.LogWarning("DokkaebiPlatformWavePreview: 발판, 분노, 3웨이브 또는 보충 적 연결이 없습니다.", this);
            yield break;
        }

        foreach (Wave wave in _waves)
        {
            IReadOnlyList<SpawnGroup> groups = wave?.GetGroups();
            if (groups == null || groups.Count == 0)
            {
                Debug.LogWarning("DokkaebiPlatformWavePreview: 비어 있는 웨이브가 있습니다.", this);
                yield break;
            }

            foreach (SpawnGroup group in groups)
            {
                if (group == null || group.Enemies == null || group.Enemies.Length == 0)
                {
                    Debug.LogWarning("DokkaebiPlatformWavePreview: 비어 있는 그룹이 있습니다.", this);
                    yield break;
                }

                foreach (SpawnEntry entry in group.Enemies)
                {
                    if (entry == null || !IsEnemyPrefabValid(entry.Prefab))
                    {
                        Debug.LogWarning("DokkaebiPlatformWavePreview: 웨이브 적 Prefab 연결이 없습니다.", this);
                        yield break;
                    }
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

        InitializeSpawnEffect();
        StartCoroutine(RunWaves());
    }

    private static bool IsEnemyPrefabValid(GameObject prefab)
    {
        if (prefab == null || !prefab.TryGetComponent(out Enemy enemy)) return false;
        return enemy.Data != null && prefab.GetComponent<BoxCollider2D>() != null;
    }

    private bool IsPlayerTouchingSurface()
    {
        if (_playerBody == null || _platformSurface == null) return false;
        if (_playerBody.IsTouching(_platformSurface)) return true;
        Bounds surfaceBounds = _platformSurface.bounds;
        surfaceBounds.Expand(new Vector3(0.2f, 0.4f, 0f));
        return _playerBody.bounds.Intersects(surfaceBounds);
    }

    private IEnumerator RunWaves()
    {
        var pollWait = new WaitForSeconds(0.2f);
        while (!IsPlayerTouchingSurface())
        {
            if (_playerHealth.IsDead) yield break;
            yield return pollWait;
        }

        OnDokkaebiPlatformBattleStarted?.Invoke();

        _rageCountAtStart = _rage.RageActivationCount;
        for (int index = 0; index < _waves.Length; index++)
        {
            Wave wave = _waves[index];
            _currentWave = index + 1;
            Debug.Log($"도깨비 발판: {_currentWave}/3 웨이브 시작", this);

            IReadOnlyList<SpawnGroup> groups = wave.GetGroups();
            for (int g = 0; g < groups.Count; g++)
            {
                SpawnGroup group = groups[g];
                if (group.Enemies != null)
                {
                    foreach (SpawnEntry entry in group.Enemies)
                    {
                        if (_playerHealth.IsDead)
                        {
                            DestroySpawnedEnemies();
                            yield break;
                        }

                        SpawnEnemy(entry.Prefab, entry.SurfaceOffset);
                        yield return new WaitForSeconds(_spawnInterval);
                    }
                }

                bool isLastGroup = g == groups.Count - 1;
                while (true)
                {
                    if (_playerHealth.IsDead)
                    {
                        DestroySpawnedEnemies();
                        yield break;
                    }

                    UpdateAliveEnemyCount();
                    if (_aliveEnemyCount == 0)
                    {
                        if (!isLastGroup)
                        {
                            yield return new WaitForSeconds(0.4f);
                            break;
                        }

                        bool hasRequiredRage = _rage.RageActivationCount - _rageCountAtStart >= wave.MinimumRageCount;
                        if (hasRequiredRage)
                        {
                            break;
                        }

                        // 원샷·마무리 공격으로 적이 먼저 소진돼도 실제 적중 기회를 계속 제공한다.
                        if (!hasRequiredRage && IsPlayerTouchingSurface())
                        {
                            _spawnReserveOnRight = !_spawnReserveOnRight;
                            SpawnEnemy(_reserveEnemyPrefab, new Vector2(_spawnReserveOnRight ? 3f : -3f, 0f));
                        }
                    }

                    yield return pollWait;
                }
            }

            if (index < _waves.Length - 1) yield return new WaitForSeconds(_waveInterval);
        }

        CameraShake.Shake(0.5f, 0.25f);
        if (!_rage.TryStartStun()) yield break;
        _isCompleted = true;
        Debug.Log("도깨비 발판: 3웨이브 방어 완료", this);
        OnDokkaebiPlatformCleared?.Invoke();
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
        CameraShake.Shake(0.5f, 0.25f);
        _isCompleted = true;
        OnDokkaebiPlatformCleared?.Invoke();
        StartCoroutine(FallAfterStun(0f));
    }

    private IEnumerator FallAfterStun(float stunPause)
    {
        if (stunPause > 0f) yield return new WaitForSeconds(stunPause);
        _isFalling = true;
        _platformSurface.enabled = false;
        StartCoroutine(DissolveSurroundingPlatforms());
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        float elapsed = 0f;
        float fallDistance = 0f;
        float initialDropVelocity = 8f;
        while (fallDistance < _fallDistance)
        {
            elapsed += Time.deltaTime;
            fallDistance = (initialDropVelocity * elapsed) + (0.5f * _fallGravity * elapsed * elapsed);
            transform.position = startPosition + new Vector3(_fallHorizontalSpeed * elapsed, -fallDistance, 0f);
            float tiltProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / _tiltDuration));
            transform.rotation = startRotation * Quaternion.AngleAxis(_fallTiltAngle * tiltProgress, Vector3.forward);
            yield return null;
        }

        _isFalling = false;
        gameObject.SetActive(false);
    }

    private IEnumerator DissolveSurroundingPlatforms()
    {
        List<GameObject> targets = GetSurroundingPlatforms();
        if (targets.Count == 0) yield break;

        var renderers = new List<SpriteRenderer>();
        var initialAlphas = new List<float>();
        foreach (GameObject platform in targets)
        {
            if (platform == null) continue;
            foreach (Collider2D col in platform.GetComponentsInChildren<Collider2D>(true))
            {
                col.enabled = false;
            }
            foreach (PlatformEffector2D effector in platform.GetComponentsInChildren<PlatformEffector2D>(true))
            {
                effector.enabled = false;
            }
            foreach (SpriteRenderer sr in platform.GetComponentsInChildren<SpriteRenderer>(true))
            {
                renderers.Add(sr);
                initialAlphas.Add(sr.color.a);
            }
        }

        float elapsed = 0f;
        while (elapsed < _platformDissolveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _platformDissolveDuration);
            float alphaFactor = 1f - Mathf.SmoothStep(0f, 1f, t);
            for (int i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] != null)
                {
                    Color c = renderers[i].color;
                    c.a = initialAlphas[i] * alphaFactor;
                    renderers[i].color = c;
                }
            }
            yield return null;
        }

        foreach (GameObject platform in targets)
        {
            if (platform != null)
            {
                platform.SetActive(false);
            }
        }
    }

    private List<GameObject> GetSurroundingPlatforms()
    {
        var result = new List<GameObject>();
        if (_surroundingPlatforms != null && _surroundingPlatforms.Length > 0)
        {
            foreach (GameObject platform in _surroundingPlatforms)
            {
                if (platform != null && !result.Contains(platform))
                    result.Add(platform);
            }
            return result;
        }

        Transform root = _surroundingPlatformsRoot;
        if (root == null)
        {
            var puzzle = GameObject.Find("Test_TimedPlatformPuzzle");
            if (puzzle != null) root = puzzle.transform;
            else if (transform.parent != null) root = transform.parent;
        }

        if (root != null)
        {
            foreach (Transform child in root)
            {
                if (child == transform) continue;
                if (child.name.Contains("MovingPlatform") || child.name.Contains("gamtoo") || child.name.Contains("Gamtu")) continue;
                if (child.name.Contains("SafetyFloor") || child.localPosition.y < 0f || child.position.y <= -20f) continue;
                if (child.name.Contains("Platform") || child.name.Contains("Safe"))
                {
                    result.Add(child.gameObject);
                }
            }
        }

        return result;
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
        if (_useIllusionEnemies)
        {
            // 생성된 인스턴스의 몸통 판정만 제거한다. 실제 공격 콜라이더는 유지한다.
            foreach (EnemyContactDamage contactDamage in instance.GetComponentsInChildren<EnemyContactDamage>(true))
            {
                contactDamage.enabled = false;
                Destroy(contactDamage);
            }
            instance.AddComponent<DokkaebiWaveIllusion>().Initialize(enemy, _illusionMaterial);
        }
        // 지상 적만 발판 가장자리 안에 제한한다. 비행 적은 공격·우회에 필요한 원본 범위를 유지한다.
        if (enemy.MoveArea != null && enemy.MoveType is EnemyGroundMove)
        {
            enemy.MoveArea.CenterOffset = new Vector2(surface.center.x - position.x, surface.max.y + 2f - position.y);
            enemy.MoveArea.AreaSize = new Vector2(Mathf.Max(1f, surface.size.x - 3f), 6f);
        }

        _spawnedEnemies.Add(enemy);
        _aliveEnemyCount++;
        PlaySpawnEffect(enemy.GetEnemyCenterPosition());
    }

    private void InitializeSpawnEffect()
    {
        if (_spawnEffectPrefab == null)
        {
            if (_useIllusionEnemies)
                Debug.LogWarning("DokkaebiPlatformWavePreview: 환영 웨이브의 Spawn Effect Prefab이 비어 있습니다.", this);
            return;
        }
        Animator animator = _spawnEffectPrefab.GetComponent<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("DokkaebiPlatformWavePreview: 등장 효과에 Animator와 Controller가 필요합니다.", this);
            return;
        }
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip != null) _spawnEffectDuration = Mathf.Max(_spawnEffectDuration, clip.length);
        }
        _spawnEffectDuration /= Mathf.Max(0.01f, animator.speed);
        if (_spawnEffectDuration <= 0f)
            Debug.LogWarning("DokkaebiPlatformWavePreview: 등장 효과의 재생 클립이 없습니다.", this);
    }

    private void PlaySpawnEffect(Vector2 position)
    {
        if (_spawnEffectPrefab == null || _spawnEffectDuration <= 0f) return;
        GameObject effect = null;
        foreach (GameObject candidate in _spawnEffects)
        {
            if (candidate != null && !candidate.activeSelf)
            {
                effect = candidate;
                break;
            }
        }
        if (effect == null)
        {
            effect = Instantiate(_spawnEffectPrefab, position, Quaternion.identity);
            effect.transform.SetParent(transform, true);
            SpriteRenderer renderer = effect.GetComponent<SpriteRenderer>();
            if (renderer != null) renderer.sortingLayerName = "Effect";
            _spawnEffects.Add(effect);
        }
        effect.transform.position = new Vector3(position.x, position.y, transform.position.z);
        effect.SetActive(true);
        Animator animator = effect.GetComponent<Animator>();
        animator.Rebind();
        animator.Play(0, 0, 0f);
        animator.Update(0f);
        StartCoroutine(StopSpawnEffectAfterPlayback(effect));
    }

    private IEnumerator StopSpawnEffectAfterPlayback(GameObject effect)
    {
        yield return new WaitForSeconds(_spawnEffectDuration);
        if (effect != null) effect.SetActive(false);
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
        foreach (GameObject effect in _spawnEffects)
        {
            if (effect != null) effect.SetActive(false);
        }
    }
}
