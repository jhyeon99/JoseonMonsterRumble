using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class DeathRespawnController : MonoBehaviour
{
    public static DeathRespawnController Instance { get; private set; }

    [SerializeField] private DeathScreenView _deathScreen;
    [SerializeField, Min(0f)] private float _displayDuration = 0.8f;

    private PlayerHealth _playerHealth;
    private string _activeSceneName;
    private string _activeCheckpointId;
    private bool _isRespawning;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;

        if (_deathScreen == null)
        {
            Debug.LogWarning($"{name}: Death Screen 참조가 없습니다.", this);
        }
    }

    private void Start()
    {
        InitializeScene(SceneManager.GetActiveScene(), false);
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

    public void ActivateCheckpoint(RespawnPoint point)
    {
        if (point == null || string.IsNullOrWhiteSpace(point.CheckpointId))
        {
            return;
        }

        string sceneName = point.gameObject.scene.name;
        if (sceneName != _activeSceneName)
        {
            return;
        }

        _activeCheckpointId = point.CheckpointId;
    }

    private void HandlePlayerDied()
    {
        if (_isRespawning)
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

        StartCoroutine(RespawnRoutine(activeScene.name));
    }

    private IEnumerator RespawnRoutine(string sceneName)
    {
        _isRespawning = true;

        if (_deathScreen != null)
        {
            yield return _deathScreen.FadeIn();
        }

        yield return new WaitForSecondsRealtime(_displayDuration);

        UnbindPlayerHealth();

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(
            sceneName,
            LoadSceneMode.Single
        );

        if (loadOperation == null)
        {
            Debug.LogWarning(
                $"{sceneName}: 씬 재로딩을 시작하지 못했습니다.",
                this
            );
            _deathScreen?.HideImmediate();
            _isRespawning = false;
            yield break;
        }

        yield return loadOperation;

        if (_deathScreen != null)
        {
            yield return _deathScreen.FadeOut();
        }

        _isRespawning = false;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        bool preserveCheckpoint =
            _isRespawning && scene.name == _activeSceneName;
        InitializeScene(scene, preserveCheckpoint);
    }

    private void InitializeScene(Scene scene, bool preserveCheckpoint)
    {
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
    }

    private void UnbindPlayerHealth()
    {
        if (_playerHealth == null)
        {
            return;
        }

        _playerHealth.OnDied -= HandlePlayerDied;
        _playerHealth = null;
    }

    private void MovePlayerTo(RespawnPoint point)
    {
        if (_playerHealth == null || point == null)
        {
            return;
        }

        Rigidbody2D playerBody = _playerHealth.GetComponent<Rigidbody2D>();
        if (playerBody == null)
        {
            _playerHealth.transform.position = point.Position;
            return;
        }

        playerBody.linearVelocity = Vector2.zero;
        playerBody.angularVelocity = 0f;
        playerBody.position = point.Position;
    }
}
