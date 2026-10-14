using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SceneArrivalFade : MonoBehaviour
{
    private static SceneArrivalFade _instance;
    private readonly List<PlayerLock> _playerLocks = new List<PlayerLock>();
    private CanvasGroup _overlay;
    private float _duration;
    private bool _isTransitioning;
    private bool _readyToReveal;
    private bool _lockPlayersDuringFade;

    public static bool IsTransitioning => _instance != null && _instance._isTransitioning;

    public static void Prepare(float duration)
    {
        SceneArrivalFade transition = GetOrCreate();
        transition._duration = Mathf.Max(0.01f, duration);
        transition._overlay.alpha = 1f;
        transition._isTransitioning = true;
        transition._readyToReveal = true;
    }

    public static IEnumerator FadeOut(float duration)
    {
        SceneArrivalFade transition = GetOrCreate();
        transition._isTransitioning = true;
        transition._lockPlayersDuringFade = true;
        transition.LockPlayers();
        float startAlpha = transition._overlay.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            transition._overlay.alpha = Mathf.Lerp(startAlpha, 1f, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        transition._overlay.alpha = 1f;
    }

    public static IEnumerator FadeIn(float duration)
    {
        SceneArrivalFade transition = GetOrCreate();
        transition._duration = Mathf.Max(0.01f, duration);
        transition.UnlockMovementOnly();
        yield return transition.Reveal();
    }

    public static void Cancel()
    {
        if (_instance == null) return;
        _instance.StopAllCoroutines();
        _instance.Complete();
    }

    private static SceneArrivalFade GetOrCreate()
    {
        if (_instance == null)
        {
            var overlayObject = new GameObject("SceneArrivalFade", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            overlayObject.AddComponent<SceneArrivalFade>();
        }
        return _instance;
    }

    private void Awake()
    {
        _instance = this;
        DontDestroyOnLoad(gameObject);
        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;
        _overlay = GetComponent<CanvasGroup>();
        _overlay.alpha = 0f;
        _overlay.blocksRaycasts = true;
        gameObject.AddComponent<GraphicRaycaster>();
        var screen = new GameObject("BlackScreen", typeof(RectTransform), typeof(Image));
        screen.transform.SetParent(transform, false);
        RectTransform rect = screen.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        screen.GetComponent<Image>().color = Color.black;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!_readyToReveal || mode != LoadSceneMode.Single) return;
        _readyToReveal = false;
        // 도착한 플레이어는 입력만 잠근다. 물리를 멈추면 착지 접촉이 사라진다.
        if (_lockPlayersDuringFade) LockPlayers(false);
        StartCoroutine(Reveal());
    }

    private IEnumerator Reveal()
    {
        // Keep the screen black through Start, spawn placement and the first camera LateUpdate.
        yield return null;
        yield return null;
        float elapsed = 0f;
        while (elapsed < _duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _overlay.alpha = 1f - Mathf.Clamp01(elapsed / _duration);
            yield return null;
        }
        Complete();
    }

    private void LockPlayers(bool freezeMovement = true)
    {
        Player[] players = FindObjectsByType<Player>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Player player in players)
        {
            if (player.gameObject.scene != SceneManager.GetActiveScene()) continue;
            if (_playerLocks.Exists(entry => entry.Player == player)) continue;
            PlayerInputHandler input = player.GetComponent<PlayerInputHandler>();
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            var playerLock = new PlayerLock
            {
                Player = player,
                Input = input,
                Body = body,
                PlayerWasEnabled = player.enabled,
                InputWasEnabled = input != null && input.enabled,
                BodyWasSimulated = body != null && body.simulated
            };
            _playerLocks.Add(playerLock);
            if (freezeMovement) player.enabled = false;
            if (input != null)
            {
                input.ClearInputState();
                input.enabled = false;
            }
            if (freezeMovement && body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.simulated = false;
            }
        }
    }

    private void UnlockMovementOnly()
    {
        for (int i = 0; i < _playerLocks.Count; i++)
        {
            PlayerLock playerLock = _playerLocks[i];
            if (playerLock.Body != null) playerLock.Body.simulated = playerLock.BodyWasSimulated;
            if (playerLock.Player != null) playerLock.Player.enabled = playerLock.PlayerWasEnabled;
        }
    }

    private void ReleasePlayers()
    {
        foreach (PlayerLock playerLock in _playerLocks)
        {
            if (playerLock.Body != null) playerLock.Body.simulated = playerLock.BodyWasSimulated;
            if (playerLock.Player != null) playerLock.Player.enabled = playerLock.PlayerWasEnabled;
            if (playerLock.Input != null)
            {
                playerLock.Input.ClearInputState();
                playerLock.Input.enabled = playerLock.InputWasEnabled;
            }
        }
        _playerLocks.Clear();
    }

    private void Complete()
    {
        _overlay.alpha = 0f;
        _overlay.blocksRaycasts = false;
        ReleasePlayers();
        _isTransitioning = false;
        if (_instance == this) _instance = null;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        ReleasePlayers();
        if (_instance == this) _instance = null;
    }

    private sealed class PlayerLock
    {
        public Player Player;
        public PlayerInputHandler Input;
        public Rigidbody2D Body;
        public bool PlayerWasEnabled;
        public bool InputWasEnabled;
        public bool BodyWasSimulated;
    }
}
