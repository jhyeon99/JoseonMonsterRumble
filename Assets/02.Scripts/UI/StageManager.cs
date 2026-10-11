using Com.LuisPedroFonseca.ProCamera2D;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 스테이지 진행 관리 (싱글톤, 씬이 바뀌어도 유지).
/// - 세이브(체크포인트) 기억: 게임 실행 중에만 유지
/// - 사망 / 재시작 시 스테이지 씬을 다시 로드하고, 세이브한 적이 있으면 세이브 포인트에서 시작
/// - 세이브 포인트보다 왼쪽에 있는 적 제거 (임시, 추후 스폰 매니저로 대체 예정)
/// </summary>
public sealed class StageManager : MonoBehaviour
{
    private const float SceneReadyProgress = 0.9f;
    private const float DefaultMinLoadingDuration = 3f;

    private enum RespawnPhase
    {
        Idle,
        DeathAnimation,
        DeathScreen,
        Loading,
        Revealing
    }

    public static StageManager Instance { get; private set; }

    [Header("사망 / 재시작 연출")]
    [SerializeField]
    private DeathScreenView _deathScreen;

    [Tooltip("사망 후 화면이 어두워지기 시작할 때까지 대기 (사망 모션을 보여주는 시간)")]
    [SerializeField, Min(0f)]
    private float _deathDelay = 1.0f;

    [Tooltip("화면이 완전히 어두워진 뒤 씬을 다시 로드하기 전까지 유지하는 시간")]
    [SerializeField, Min(0f)]
    private float _displayDuration = 0.8f;

    [Tooltip("로딩 화면이 완전히 사라진 뒤 유지하는 리스폰 보호 시간")]
    [SerializeField, Min(0f)]
    private float _respawnProtectionDuration = 1.5f;

    [Header("디버그 (에디터 전용)")]

    [Tooltip("TODO: 테스트용. 에디터에서만 R 키로 스테이지 재시작 (빌드에는 포함되지 않음)")]
#pragma warning disable 0414 // 빌드에서는 사용하지 않는 필드 (직렬화 구조를 에디터와 맞추기 위해 남겨둠)
    [SerializeField]
    private bool _enableDebugRestartKey = false;
#pragma warning restore 0414

    [Header("적 정리 (임시)")]
    [Tooltip("세이브 포인트 x + 이 값보다 왼쪽에 있는 적을 제거. 음수로 하면 세이브 포인트 바로 왼쪽 적은 남김")]
    [SerializeField]
    private float _enemyClearOffsetX = 0f;

    private PlayerHealth _playerHealth;
    private string _activeSceneName;
    private string _activeCheckpointId;
    [SerializeField, ReadOnly] private RespawnPhase _respawnPhase;
    private Coroutine _respawnProtectionCoroutine;
    private bool _isChangingScene;

    public bool IsRespawning => _respawnPhase != RespawnPhase.Idle;
    public bool IsRespawnWorldFrozen => _respawnPhase == RespawnPhase.DeathScreen ||
                                       _respawnPhase == RespawnPhase.Loading ||
                                       _respawnPhase == RespawnPhase.Revealing;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _respawnPhase = RespawnPhase.Idle;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;

        if (_deathScreen == null)
        {
            _deathScreen = FindFirstObjectByType<DeathScreenView>(FindObjectsInactive.Include);
        }

        if (_deathScreen == null)
        {
            Debug.LogWarning($"{name}: Death Screen 참조가 없습니다.", this);
        }
    }

    private void Start()
    {
        InitializeScene(SceneManager.GetActiveScene(), false);
    }

    private void Update()
    {
        // TODO: 테스트용 임시 코드.
        // 일시정지 메뉴에 재시작 버튼이 생기면 제거
        if (_enableDebugRestartKey && Input.GetKeyDown(KeyCode.R) && !PauseController.IsPaused)
        {
            RestartStage();
        }

        if (Input.GetKeyDown(KeyCode.F12))
        {
            TryLoadStageScene("Test_Boss");
        }
    }

    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        UnbindPlayerHealth();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        Instance = null;
    }

    private void OnDisable()
    {
        if (Instance != this)
        {
            return;
        }
        StopAllCoroutines();
        StopRespawnProtection();
        ResetRespawnTransition();
    }

    private void LateUpdate()
    {
        // Scene initialization and cinematic scripts may restore time during loading.
        if (IsRespawnWorldFrozen && !Mathf.Approximately(Time.timeScale, 0f))
        {
            Time.timeScale = 0f;
        }
    }

    /// <summary>
    /// 체크포인트 활성화. 새로 세이브된 경우에만 true (세이브 연출 여부 판단용).
    /// </summary>
    public bool ActivateCheckpoint(RespawnPoint point)
    {
        if (point == null || string.IsNullOrWhiteSpace(point.CheckpointId))
        {
            return false;
        }

        if (IsRespawning || _isChangingScene)
        {
            return false;
        }

        string sceneName = point.gameObject.scene.name;
        if (sceneName != _activeSceneName)
        {
            return false;
        }

        if (point.CheckpointId == _activeCheckpointId)
        {
            return false;
        }

        _activeCheckpointId = point.CheckpointId;
        if (_playerHealth != null)
        {
            _playerHealth.RefillPotions();
        }
        RefreshCheckpointVisuals(point.gameObject.scene);
        return true;
    }

    /// <summary>
    /// 스테이지 재시작 (일시정지 메뉴 등에서 호출). 세이브한 적이 있으면 세이브 포인트에서 시작.
    /// </summary>
    public void RestartStage()
    {
        TryStartRespawn(false, string.Empty);
    }

    /// <summary>지정한 스테이지 씬으로 전환한다. 중복 전환과 리스폰 중 전환은 막는다.</summary>
    public bool TryLoadStageScene(string sceneName, float fadeOutDuration = 0.5f, float fadeInDuration = 0.75f)
    {
        if (IsRespawning || _isChangingScene || SceneArrivalFade.IsTransitioning || string.IsNullOrWhiteSpace(sceneName))
            return false;

        if (SceneManager.GetActiveScene().name == sceneName)
            return false;

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning($"{name}: '{sceneName}' 씬을 Build Settings에서 찾을 수 없습니다.", this);
            return false;
        }

        StartCoroutine(ChangeStageSceneRoutine(sceneName, fadeOutDuration, fadeInDuration));
        return true;
    }

    private IEnumerator ChangeStageSceneRoutine(string sceneName, float fadeOutDuration, float fadeInDuration)
    {
        _isChangingScene = true;
        CameraShake.Stop();
        Time.timeScale = 1f;
        yield return SceneArrivalFade.FadeOut(Mathf.Max(0f, fadeOutDuration));
        SceneArrivalFade.Prepare(fadeInDuration);
        UnbindPlayerHealth();

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (loadOperation == null)
        {
            Debug.LogWarning($"{sceneName}: 씬 전환을 시작하지 못했습니다.", this);
            SceneArrivalFade.Cancel();
            BindPlayerHealth(SceneManager.GetActiveScene());
            _isChangingScene = false;
            yield break;
        }

        loadOperation.allowSceneActivation = false;
        while (loadOperation.progress < 0.9f) yield return null;
        Time.timeScale = 1f;
        loadOperation.allowSceneActivation = true;
        yield return loadOperation;
        while (SceneArrivalFade.IsTransitioning) yield return null;
        _isChangingScene = false;
    }

    private void HandlePlayerDied()
    {
        string causeOfDeath = _playerHealth != null ? _playerHealth.LastFatalHitData.AttackName : string.Empty;
        if (string.IsNullOrWhiteSpace(causeOfDeath))
        {
            causeOfDeath = "알 수 없는 원인";
        }

        TryStartRespawn(true, causeOfDeath);
    }

    private void TryStartRespawn(bool waitForDeath, string causeOfDeath = "")
    {
        if (IsRespawning || _isChangingScene)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.buildIndex < 0)
        {
            Debug.LogWarning(
                $"{activeScene.name}: Build Settings에 등록되지 않아 리스폰할 수 없습니다.",
                this
            );
            return;
        }

        StartCoroutine(RespawnRoutine(activeScene.name, waitForDeath, causeOfDeath));
    }

    private IEnumerator RespawnRoutine(string sceneName, bool waitForDeath, string causeOfDeath)
    {
        StopRespawnProtection();
        _respawnPhase = RespawnPhase.DeathAnimation;
        Coroutine loadingAnimCoroutine = null;
        bool completed = false;
        try
        {
            if (waitForDeath)
            {
                yield return new WaitForSecondsRealtime(_deathDelay);
            }

            _respawnPhase = RespawnPhase.DeathScreen;
            HitStop.Stop();
            CameraShake.Stop();
            PauseController pauseController = FindFirstObjectByType<PauseController>();
            if (pauseController != null)
            {
                pauseController.ResumeGame();
            }
            Time.timeScale = 0f;
            if (_playerHealth != null)
            {
                _playerHealth.SetRespawnProtected(true);
            }

            if (_deathScreen == null)
            {
                _deathScreen = FindFirstObjectByType<DeathScreenView>(FindObjectsInactive.Include);
            }
            if (_deathScreen != null && waitForDeath)
            {
                _deathScreen.SetCauseOfDeath(causeOfDeath);
                yield return _deathScreen.FadeIn();
                yield return _deathScreen.WaitForRestartInput();
            }
            else if (waitForDeath)
            {
                yield return new WaitForSecondsRealtime(_displayDuration);
            }

            _respawnPhase = RespawnPhase.Loading;
            UnbindPlayerHealth();
            if (_deathScreen != null)
            {
                loadingAnimCoroutine = StartCoroutine(_deathScreen.PlayLoadingRoutine());
            }
            float minLoadingTime = _deathScreen != null
                ? _deathScreen.MinLoadingDuration : DefaultMinLoadingDuration;
            float loadingStartTime = Time.realtimeSinceStartup;
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (loadOperation == null)
            {
                Debug.LogWarning($"{sceneName}: 씬 재로딩을 시작하지 못했습니다.", this);
                BindPlayerHealth(SceneManager.GetActiveScene());
                yield break;
            }
            loadOperation.allowSceneActivation = false;
            while (loadOperation.progress < SceneReadyProgress ||
                   Time.realtimeSinceStartup - loadingStartTime < minLoadingTime)
            {
                yield return null;
            }
            loadOperation.allowSceneActivation = true;
            yield return loadOperation;
            // sceneLoaded applies protection and position before Start; let startup finish
            // beneath the loading screen without advancing physics or AI timers.
            yield return null;

            _respawnPhase = RespawnPhase.Revealing;
            if (loadingAnimCoroutine != null)
            {
                StopCoroutine(loadingAnimCoroutine);
                loadingAnimCoroutine = null;
            }
            if (_deathScreen != null)
            {
                yield return _deathScreen.FadeOutLoading();
            }
            completed = true;
        }
        finally
        {
            if (loadingAnimCoroutine != null)
            {
                StopCoroutine(loadingAnimCoroutine);
            }
            _deathScreen?.HideImmediate();
            ResetRespawnTransition();
            if (!completed)
            {
                StopRespawnProtection();
            }
        }

        if (_playerHealth != null)
        {
            if (_respawnProtectionDuration > 0f)
            {
                _respawnProtectionCoroutine = StartCoroutine(ReleaseRespawnProtectionRoutine(_playerHealth));
            }
            else
            {
                _playerHealth.SetRespawnProtected(false);
            }
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        bool preserveCheckpoint =
            IsRespawning && scene.name == _activeSceneName;
        InitializeScene(scene, preserveCheckpoint);
    }

    private void InitializeScene(Scene scene, bool preserveCheckpoint)
    {
        if (_deathScreen == null)
        {
            _deathScreen = FindFirstObjectByType<DeathScreenView>(FindObjectsInactive.Include);
        }

        RespawnPoint[] points = FindSceneRespawnPoints(scene);
        if (points.Length == 0)
        {
            UnbindPlayerHealth();
            _activeSceneName = string.Empty;
            _activeCheckpointId = string.Empty;
            _deathScreen?.HideImmediate();
            return;
        }

        RespawnPoint defaultPoint = ValidateAndFindDefault(points, scene.name);

        if (!preserveCheckpoint || _activeSceneName != scene.name)
        {
            _activeSceneName = scene.name;
            _activeCheckpointId = defaultPoint != null
                ? defaultPoint.CheckpointId
                : string.Empty;
        }

        RespawnPoint targetPoint = FindPoint(
            points,
            _activeCheckpointId
        );

        if (targetPoint == null)
        {
            targetPoint = defaultPoint;
            _activeCheckpointId = defaultPoint != null
                ? defaultPoint.CheckpointId
                : string.Empty;

            Debug.LogWarning(
                $"{scene.name}: 활성 체크포인트를 찾지 못해 기본 시작점을 사용합니다.",
                this
            );
        }

        BindPlayerHealth(scene);
        MovePlayerTo(targetPoint);
        RefreshCheckpointVisuals(points);

        // 세이브 포인트에서 시작할 때만 왼쪽 적 정리 (기본 시작점이면 건드리지 않음)
        if (targetPoint != null && !targetPoint.IsDefault)
        {
            ClearEnemiesBehind(scene, targetPoint);
        }
    }

    // 씬 로드 직후(적의 Start 전)에 호출되어야 EnemyManager에 등록되기 전에 꺼짐
    private void ClearEnemiesBehind(Scene scene, RespawnPoint point)
    {
        float limitX = point.Position.x + _enemyClearOffsetX;

        Enemy[] enemies = FindObjectsByType<Enemy>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < enemies.Length; i++)
        {
            Enemy enemy = enemies[i];
            if (enemy.gameObject.scene != scene)
            {
                continue;
            }

            if (enemy.transform.position.x < limitX)
            {
                // Destroy나 사망 처리 대신 비활성화 (아이템 드롭 등 사망 로직이 돌지 않도록)
                enemy.gameObject.SetActive(false);
            }
        }
    }

    private void RefreshCheckpointVisuals(Scene scene)
    {
        RefreshCheckpointVisuals(FindSceneRespawnPoints(scene));
    }

    private void RefreshCheckpointVisuals(RespawnPoint[] points)
    {
        for (int i = 0; i < points.Length; i++)
        {
            points[i].SetActivated(points[i].CheckpointId == _activeCheckpointId);
        }
    }

    private RespawnPoint[] FindSceneRespawnPoints(Scene scene)
    {
        RespawnPoint[] allPoints = FindObjectsByType<RespawnPoint>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        var scenePoints = new List<RespawnPoint>();

        for (int i = 0; i < allPoints.Length; i++)
        {
            if (allPoints[i].gameObject.scene == scene)
            {
                scenePoints.Add(allPoints[i]);
            }
        }

        return scenePoints.ToArray();
    }

    private RespawnPoint ValidateAndFindDefault(
        RespawnPoint[] points,
        string sceneName
    )
    {
        var checkpointIds = new HashSet<string>();
        RespawnPoint defaultPoint = null;
        int defaultCount = 0;

        for (int i = 0; i < points.Length; i++)
        {
            RespawnPoint point = points[i];

            if (!checkpointIds.Add(point.CheckpointId))
            {
                Debug.LogWarning(
                    $"{sceneName}: Checkpoint ID '{point.CheckpointId}'가 중복됩니다.",
                    point
                );
            }

            if (!point.IsDefault)
            {
                continue;
            }

            defaultCount++;
            defaultPoint ??= point;
        }

        if (defaultCount != 1)
        {
            Debug.LogWarning(
                $"{sceneName}: 기본 RespawnPoint는 정확히 1개여야 합니다. 현재 {defaultCount}개입니다.",
                this
            );
        }

        return defaultPoint;
    }

    private RespawnPoint FindPoint(
        RespawnPoint[] points,
        string checkpointId
    )
    {
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i].CheckpointId == checkpointId)
            {
                return points[i];
            }
        }

        return null;
    }

    private void BindPlayerHealth(Scene scene)
    {
        UnbindPlayerHealth();

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i].gameObject.scene == scene)
            {
                _playerHealth = players[i];
                break;
            }
        }

        if (_playerHealth == null)
        {
            Debug.LogWarning(
                $"{scene.name}: PlayerHealth를 찾지 못했습니다.",
                this
            );
            return;
        }

        _playerHealth.OnDied += HandlePlayerDied;
        _playerHealth.RefillPotions();
        if (IsRespawning)
        {
            _playerHealth.SetRespawnProtected(true);
        }
    }

    private void UnbindPlayerHealth()
    {
        StopRespawnProtection();
        if (_playerHealth == null)
        {
            return;
        }

        _playerHealth.OnDied -= HandlePlayerDied;
        _playerHealth = null;
    }

    private IEnumerator ReleaseRespawnProtectionRoutine(PlayerHealth playerHealth)
    {
        yield return new WaitForSeconds(_respawnProtectionDuration);
        if (playerHealth != null)
        {
            playerHealth.SetRespawnProtected(false);
        }
        _respawnProtectionCoroutine = null;
    }

    private void StopRespawnProtection()
    {
        if (_respawnProtectionCoroutine != null)
        {
            StopCoroutine(_respawnProtectionCoroutine);
            _respawnProtectionCoroutine = null;
        }
        if (_playerHealth != null)
        {
            _playerHealth.SetRespawnProtected(false);
        }
    }

    private void ResetRespawnTransition()
    {
        bool wasFrozen = IsRespawnWorldFrozen;
        _respawnPhase = RespawnPhase.Idle;
        if (wasFrozen)
        {
            HitStop.Stop();
            Time.timeScale = PauseController.IsPaused ? 0f : 1f;
        }
    }

    private void MovePlayerTo(RespawnPoint point)
    {
        if (_playerHealth == null || point == null)
        {
            return;
        }

        // Rigidbody 위치는 다음 물리 스텝에 Transform에 반영되므로 Transform도 같이 옮김 (카메라 스냅용)
        _playerHealth.transform.position = point.Position;

        Rigidbody2D playerBody = _playerHealth.GetComponent<Rigidbody2D>();
        if (playerBody != null)
        {
            playerBody.linearVelocity = Vector2.zero;
            playerBody.angularVelocity = 0f;
            playerBody.position = point.Position;
        }

        // 카메라가 시작 위치에서 미끄러져 오지 않도록 바로 붙임
        Camera mainCamera = Camera.main;
        ProCamera2D proCamera = mainCamera != null ? mainCamera.GetComponent<ProCamera2D>() : null;
        if (proCamera != null && proCamera.isActiveAndEnabled)
        {
            proCamera.Reset();
            return;
        }

        CameraFollow cameraFollow = FindFirstObjectByType<CameraFollow>();
        if (cameraFollow != null)
        {
            cameraFollow.SnapToTarget();
        }
    }
}
