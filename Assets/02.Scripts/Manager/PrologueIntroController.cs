using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Com.LuisPedroFonseca.ProCamera2D;

// Attach to an always-active scene-root object, outside both locations.
public class PrologueIntroController : MonoBehaviour
{
    [Header("Player and camera")]
    [SerializeField] private Player _player;
    [SerializeField] private ProCamera2D _camera;
    [SerializeField] private ProCamera2DCinematics _cinematics;
    [SerializeField] private Transform _location2Spawn;

    [Header("Location transition")]
    [Tooltip("시작 위치에서 이 지점의 X좌표를 넘어가면 장소 2로 전환합니다.")]
    [SerializeField] private Transform _location2TransitionPoint;

    [Header("Opening sounds (assign later)")]
    [SerializeField] private AudioClip _screamClip;
    [Tooltip("검은 화면에서 비명 소리와 동시에 재생할 지게 부서지는 소리입니다.")]
    [SerializeField] private AudioClip _jigeBreakClip;
    [SerializeField] private AudioClip _doorClip;
    [SerializeField, Min(0.1f)] private float _screamWaitWithoutClip = 1.5f;

    [Header("Broken jige inspection")]
    [SerializeField] private Transform _jige;
    [SerializeField, Min(0.1f)] private float _jigeTriggerDistance = 3.5f;
    [SerializeField, Min(1f)] private float _jigeZoom = 2f;
    [SerializeField, Min(0.1f)] private float _jigePanDuration = 0.8f;

    [Header("Dialogue")]
    [SerializeField] private Font _subtitleFont;
    [SerializeField, Min(0.1f)] private float _whatDuration = 1f;
    [SerializeField, Min(0.1f)] private float _inspectionDuration = 2f;
    [SerializeField, Min(0.1f)] private float _forwardDuration = 1.5f;

    [Header("Backgrounds (required)")]
    [SerializeField] private GameObject _background1;
    [SerializeField] private GameObject _background2;

    [Header("Environment groups (optional)")]
    [SerializeField] private GameObject _environment1;
    [SerializeField] private GameObject _environment2;

    [Header("Transition presentation")]
    [SerializeField] private GameObject _gameplayUi;
    [SerializeField] private bool _alignBackground2ToCamera = true;

    public string Phase { get; private set; } = "NotStarted";
    public bool IsIntroComplete { get; private set; }
    public bool IsTransitionComplete => Phase == "Complete";
    public Vector3 LastTransferPosition { get; private set; }
    public bool IsJigeInspected { get; private set; }
    public string CurrentSubtitle => _subtitle != null ? _subtitle.text : "";

    [Header("Fade durations")]
    [SerializeField, Min(0.01f)] private float _openingFade = 1f;
    [SerializeField, Min(0.01f)] private float _fadeToBlack = 0.5f;
    [SerializeField, Min(0.01f)] private float _revealLocation2 = 0.75f;

    private PlayerInputHandler _input;
    private Rigidbody2D _body;
    private CanvasGroup _fade;
    private readonly Dictionary<Transform, Vector3> _background2Layout =
        new Dictionary<Transform, Vector3>();
    private bool _inputWasEnabled;
    private bool _playerWasEnabled;
    private bool _bodyWasSimulated;
    private bool _locked;
    private bool _cinematicFinished;
    private bool _ready;
    private bool _uiWasActive;
    private bool _uiCaptured;
    private float _transitionDirection;
    private bool _jigeReached;
    private Text _subtitle;
    private Transform _jigeFocus;
    private readonly HashSet<ParallaxManager> _destinationParallax =
        new HashSet<ParallaxManager>();

    private void Awake()
    {
        if (_player == null || _camera == null || _cinematics == null ||
            _location2Spawn == null || _location2TransitionPoint == null ||
            _background1 == null || _background2 == null || _jige == null)
        {
            Debug.LogError("Prologue: Player, Camera, Cinematics, Jige, Location 2 Spawn/Transition Point, Background 1/2를 연결하세요.", this);
            enabled = false;
            return;
        }

        if (_cinematics.gameObject != _camera.gameObject || !_camera.enabled || !_cinematics.enabled)
        {
            Debug.LogError("Prologue: Main Camera의 ProCamera2D와 Cinematics를 함께 활성화하세요.", this);
            enabled = false;
            return;
        }
        GameObject[] roots = new[] { _background1, _background2, _environment1, _environment2 };
        foreach (GameObject root in roots)
        {
            if (root == null) continue;
            if (transform.IsChildOf(root.transform) || _player.transform.IsChildOf(root.transform) ||
                _camera.transform.IsChildOf(root.transform))
            {
                Debug.LogError("Prologue: IntroController, Player, Main Camera는 장소 묶음 밖의 씬 루트에 두세요.", this);
                enabled = false;
                return;
            }
        }
        GameObject[] sources = { _background1, _environment1 };
        GameObject[] destinations = { _background2, _environment2 };
        foreach (GameObject source in sources)
        {
            foreach (GameObject destination in destinations)
            {
                if (source != null && destination != null &&
                    (source.transform.IsChildOf(destination.transform) || destination.transform.IsChildOf(source.transform)))
                {
                    Debug.LogError("Prologue: 장소 1과 장소 2 묶음은 서로 다른 오브젝트로 분리하세요.", this);
                    enabled = false;
                    return;
                }
            }
        }

        _input = _player.GetComponent<PlayerInputHandler>();
        _body = _player.GetComponent<Rigidbody2D>();
        if (_input == null || _body == null || !_player.enabled)
        {
            Debug.LogError("Prologue: 활성화된 Player와 PlayerInputHandler, Rigidbody2D가 필요합니다.", this);
            enabled = false;
            return;
        }

        // Capture the authored layout before any parallax LateUpdate can move it.
        foreach (Transform child in _background2.GetComponentsInChildren<Transform>(true))
            _background2Layout[child] = child.localPosition;
        // The two background sprites can share a manager on their parent.
        foreach (ParallaxManager manager in _background2.GetComponentsInParent<ParallaxManager>(true))
            _destinationParallax.Add(manager);
        foreach (ParallaxManager manager in _background2.GetComponentsInChildren<ParallaxManager>(true))
            _destinationParallax.Add(manager);
        foreach (ParallaxManager manager in _destinationParallax)
        {
            foreach (Transform child in manager.GetComponentsInChildren<Transform>(true))
                _background2Layout[child] = child.localPosition;
        }

        _inputWasEnabled = _input.enabled;
        _playerWasEnabled = _player.enabled;
        _bodyWasSimulated = _body.simulated;
        _input.ClearInputState();
        _input.enabled = false;
        _locked = true;
        Phase = "Preparing";
        _cinematics.OnCinematicFinished.AddListener(HandleCinematicFinished);
        _cinematics.OnCinematicTargetReached.AddListener(HandleJigeReached);

        if (_gameplayUi != null)
        {
            _uiWasActive = _gameplayUi.activeSelf;
            _uiCaptured = true;
            _gameplayUi.SetActive(false);
        }

        SetActive(_background1, true);
        SetActive(_environment1, true);
        SetActive(_background2, false);
        SetActive(_environment2, false);

        CameraFollow legacyFollow = _camera.GetComponent<CameraFollow>();
        if (legacyFollow != null) legacyFollow.enabled = false;
        if (_camera.GetCameraTarget(_player.transform) == null)
            _camera.AddCameraTarget(_player.transform);

        CreateFadeOverlay();
        _jigeFocus = new GameObject("JigeCameraFocus").transform;
        _jigeFocus.SetParent(transform, false);
        _ready = true;
    }

    private IEnumerator Start()
    {
        if (!_ready) yield break;
        // Allow Player.Start and StageManager.Start to initialize under the black overlay.
        yield return null;
        _transitionDirection = _location2TransitionPoint.position.x >= _player.transform.position.x ? 1f : -1f;
        _player.RangedAttack?.CancelCharge();
        _player.StateMachine.ChangeState(_player.IdleState);
        _body.linearVelocity = Vector2.zero;
        _body.angularVelocity = 0f;
        // Block input only. Keep gravity, contacts, idle animation and grounding alive.

        _camera.Reset(true, true, true);
        Phase = "Scream";
        ShowSubtitle("[부서지는 소리와 남자의 비명]");
        PlaySound(_screamClip);
        PlaySound(_jigeBreakClip);
        float openingSoundDuration = Mathf.Max(
            _screamClip != null ? _screamClip.length : 0f,
            _jigeBreakClip != null ? _jigeBreakClip.length : 0f);
        yield return new WaitForSecondsRealtime(openingSoundDuration > 0f ? openingSoundDuration : _screamWaitWithoutClip);
        Phase = "What";
        ShowSubtitle("뭐야?");
        yield return new WaitForSecondsRealtime(_whatDuration);
        ShowSubtitle("");
        Phase = "DoorOpening";
        PlaySound(_doorClip, 1.5f);
        // Keep the screen black until the last second of the door sound.
        float doorRevealDelay = _doorClip != null ? Mathf.Max(0f, _doorClip.length - 1f) : 0f;
        if (doorRevealDelay > 0f)
            yield return new WaitForSecondsRealtime(doorRevealDelay);
        // Player appears outside the house and can move as soon as the reveal starts.
        UnlockPlayer();
        _fade.blocksRaycasts = false;
        RestoreGameplayUi();
        IsIntroComplete = true;
        yield return FadeTo(0f, _openingFade);
        Phase = "Gameplay";
        float approachDirection = _jige.position.x >= _player.transform.position.x ? 1f : -1f;
        while ((_player.transform.position.x - (_jige.position.x - _jigeTriggerDistance * approachDirection)) * approachDirection < 0f)
            yield return null;

        LockPlayer();
        Phase = "Inspecting";
        _cinematicFinished = false;
        _jigeReached = false;
        _jigeFocus.position = _jige.position;
        SpriteRenderer jigeRenderer = _jige.GetComponentInChildren<SpriteRenderer>();
        if (jigeRenderer != null) _jigeFocus.position = jigeRenderer.bounds.center;
        _cinematics.CinematicTargets = new List<CinematicTarget> {
            new CinematicTarget { TargetTransform = _jigeFocus, EaseInDuration = _jigePanDuration,
                HoldDuration = _inspectionDuration, Zoom = _jigeZoom, EaseType = EaseType.EaseInOut }
        };
        _cinematics.Play();
        while (!_jigeReached) yield return null;
        Phase = "JigeDialogue";
        ShowSubtitle("지게가 부서져 있다.");
        yield return new WaitForSecondsRealtime(_inspectionDuration);
        ShowSubtitle("");
        Phase = "ReturningToPlayer";
        while (!_cinematicFinished) yield return null;
        Phase = "ForwardDialogue";
        ShowSubtitle("앞으로 더 가보자.");
        yield return new WaitForSecondsRealtime(_forwardDuration);
        ShowSubtitle("");
        IsJigeInspected = true;
        UnlockPlayer();
        _fade.blocksRaycasts = false;
        RestoreGameplayUi();
        Phase = "GameplayAfterJige";
        while ((_player.transform.position.x - _location2TransitionPoint.position.x) * _transitionDirection < 0f)
            yield return null;

        LockPlayer();
        Phase = "FadeToBlack";
        yield return FadeTo(1f, _fadeToBlack);

        // Swap only while completely covered. Keep the player's authored Z depth.
        Phase = "Switching";
        SetActive(_background1, false);
        SetActive(_environment1, false);
        Vector3 position = _location2Spawn.position;
        position.z = _player.transform.position.z;
        _body.linearVelocity = Vector2.zero;
        _body.angularVelocity = 0f;
        _body.position = new Vector2(position.x, position.y);
        _player.transform.position = position;
        LastTransferPosition = position;
        _player.Movement.SetMovingPlatform(null);
        Physics2D.SyncTransforms();

        // Reset both the visible camera and its internal smoothing state.
        _camera.Reset(true, true, true);
        foreach (KeyValuePair<Transform, Vector3> entry in _background2Layout)
            if (entry.Key != null) entry.Key.localPosition = entry.Value;
        SetActive(_background2, true);
        SetActive(_environment2, true);
        if (_alignBackground2ToCamera) AlignDestinationBackground();
        foreach (ParallaxManager parallax in _destinationParallax)
            parallax.RebaseAtCurrentPosition();

        // Allow new renderers and camera extensions to update beneath the black screen.
        yield return null;
        Phase = "Revealing";
        yield return FadeTo(0f, _revealLocation2);
        UnlockPlayer();
        _fade.blocksRaycasts = false;
        RestoreGameplayUi();
        Phase = "Complete";
    }

    private void AlignDestinationBackground()
    {
        SpriteRenderer[] renderers = _background2.GetComponentsInChildren<SpriteRenderer>();
        bool hasBounds = false;
        var bounds = new Bounds();
        foreach (SpriteRenderer renderer in renderers)
        {
            if (!renderer.enabled || renderer.sprite == null) continue;
            if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        if (!hasBounds) return;
        Vector3 delta = _camera.transform.position - bounds.center;
        delta.z = 0f;
        _background2.transform.position += delta;
    }

    private void RestoreGameplayUi()
    {
        if (_uiCaptured && _gameplayUi != null)
            _gameplayUi.SetActive(_uiWasActive);
        _uiCaptured = false;
    }

    private void LockPlayer()
    {
        _inputWasEnabled = _input.enabled;
        _playerWasEnabled = _player.enabled;
        _bodyWasSimulated = _body.simulated;
        _input.ClearInputState();
        _input.enabled = false;
        _player.RangedAttack?.CancelCharge();
        _player.StateMachine.ChangeState(_player.IdleState);
        _body.linearVelocity = Vector2.zero;
        _locked = true;
        _fade.blocksRaycasts = true;
        if (_gameplayUi != null)
        {
            _uiWasActive = _gameplayUi.activeSelf;
            _uiCaptured = true;
            _gameplayUi.SetActive(false);
        }
    }

    private void HandleCinematicFinished()
    {
        _cinematicFinished = true;
    }

    private void HandleJigeReached(int targetIndex) => _jigeReached = true;

    private static void PlaySound(AudioClip clip, float gain = 1f)
    {
        if (clip != null) AudioManager.Instance.PlaySfx(clip, gain: gain);
    }

    private void ShowSubtitle(string text) => _subtitle.text = text;

    private void CreateFadeOverlay()
    {
        var canvasObject = new GameObject("PrologueFade", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32766;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        _fade = canvasObject.GetComponent<CanvasGroup>();
        _fade.alpha = 1f;
        _fade.blocksRaycasts = true;

        var panel = new GameObject("BlackPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = Color.black;

        // Keep dialogue separate from the fading black panel so it also appears over black.
        var subtitleCanvasObject = new GameObject("PrologueDialogue", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        subtitleCanvasObject.transform.SetParent(transform, false);
        Canvas subtitleCanvas = subtitleCanvasObject.GetComponent<Canvas>();
        subtitleCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        subtitleCanvas.overrideSorting = true;
        subtitleCanvas.sortingOrder = 32767;
        CanvasScaler subtitleScaler = subtitleCanvasObject.GetComponent<CanvasScaler>();
        subtitleScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        subtitleScaler.referenceResolution = new Vector2(1920, 1080);
        subtitleScaler.matchWidthOrHeight = 0.5f;
        var subtitleObject = new GameObject("Subtitle", typeof(RectTransform), typeof(Text), typeof(Outline));
        subtitleObject.transform.SetParent(subtitleCanvasObject.transform, false);
        RectTransform subtitleRect = subtitleObject.GetComponent<RectTransform>();
        subtitleRect.anchorMin = new Vector2(0.1f, 0f);
        subtitleRect.anchorMax = new Vector2(0.9f, 0.2f);
        subtitleRect.offsetMin = new Vector2(0f, 48f);
        subtitleRect.offsetMax = Vector2.zero;
        _subtitle = subtitleObject.GetComponent<Text>();
        _subtitle.font = _subtitleFont != null ? _subtitleFont :
            Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 36);
        _subtitle.fontSize = 36;
        _subtitle.alignment = TextAnchor.MiddleCenter;
        _subtitle.color = Color.white;
        _subtitle.raycastTarget = false;
        subtitleObject.GetComponent<Outline>().effectDistance = new Vector2(2f, -2f);
    }

    private IEnumerator FadeTo(float alpha, float duration)
    {
        float initial = _fade.alpha;
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _fade.alpha = Mathf.Lerp(initial, alpha, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        _fade.alpha = alpha;
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null) target.SetActive(active);
    }

    private void UnlockPlayer()
    {
        if (!_locked) return;
        if (_body != null) _body.simulated = _bodyWasSimulated;
        if (_player != null) _player.enabled = _playerWasEnabled;
        if (_input != null) _input.enabled = _inputWasEnabled;
        _locked = false;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (_cinematics != null)
        {
            _cinematics.OnCinematicFinished.RemoveListener(HandleCinematicFinished);
            _cinematics.OnCinematicTargetReached.RemoveListener(HandleJigeReached);
            if (_locked && _cinematics.IsPlaying) _cinematics.Stop();
        }
        UnlockPlayer();
        RestoreGameplayUi();
        if (_subtitle != null) ShowSubtitle("");
        if (_fade != null)
        {
            _fade.alpha = 0f;
            _fade.blocksRaycasts = false;
        }
    }
}
