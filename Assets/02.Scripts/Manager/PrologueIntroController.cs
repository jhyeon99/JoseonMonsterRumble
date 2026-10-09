using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
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

    [Header("Next scene exit")]
    [Tooltip("배경 2로 이동한 뒤 이 오브젝트에 가까워지면 다음 씬으로 넘어갑니다. 비워두면 전환하지 않습니다.")]
    [SerializeField] private Transform _nextSceneExitPoint;
    [SerializeField] private string _nextSceneName = "Test_Ep1";
    [Tooltip("출구 지점과 플레이어 사이의 거리입니다. X/Y 거리만 사용합니다.")]
    [SerializeField, Min(0.1f)] private float _nextSceneTriggerDistance = 1.5f;
    [Tooltip("다음 씬을 불러온 뒤 검은 화면이 밝아지는 시간입니다.")]
    [SerializeField, Min(0.01f)] private float _nextSceneRevealDuration = 0.75f;

    [Header("Opening sounds (assign later)")]
    [SerializeField] private AudioClip _screamClip;
    [Tooltip("검은 화면에서 비명 소리와 동시에 재생할 지게 부서지는 소리입니다.")]
    [SerializeField] private AudioClip _jigeBreakClip;
    [SerializeField] private AudioClip _doorClip;

    [Header("Broken jige inspection")]
    [SerializeField] private Transform _jige;
    [SerializeField, Min(0.1f)] private float _jigeTriggerDistance = 3.5f;
    [SerializeField, Min(1f)] private float _jigeZoom = 2f;
    [SerializeField, Min(0.1f)] private float _jigePanDuration = 0.8f;

    [Header("Dialogue")]
    [SerializeField] private Font _subtitleFont;
    [SerializeField, Min(0.1f)] private float _inspectionDuration = 2f;
    [SerializeField, Min(0.1f)] private float _forwardDuration = 1.5f;

    [Header("Dialogue images (optional)")]
    [Tooltip("하단 대사창의 배경 그림입니다. 비워두면 기본 디자인을 사용합니다.")]
    [SerializeField] private Sprite _dialoguePanelSprite;
    [Tooltip("오른쪽 위 선택지의 배경 그림입니다. 선택지 글자는 별도로 표시합니다.")]
    [SerializeField] private Sprite _choicePanelSprite;
    [Tooltip("Enter 문구와 화살표가 포함된 완성된 키 이미지입니다. 넣으면 기본 Enter 그림을 대체합니다.")]
    [SerializeField] private Sprite _enterKeySprite;

    [Header("Dialogue layout")]
    [SerializeField, Min(180f)] private float _dialogueHeight = 280f;
    [SerializeField, Min(0f)] private float _dialogueBottomMargin = 48f;
    [SerializeField, Min(0f)] private float _dialogueSideMargin = 64f;
    [SerializeField, Min(18)] private int _dialogueFontSize = 48;
    [SerializeField] private Color _dialogueTextColor = new Color(0.94f, 0.93f, 0.88f, 1f);
    [SerializeField] private Color _choiceTextColor = new Color(1f, 0.88f, 0.65f, 1f);
    [SerializeField] private Color _dialoguePanelColor = new Color(0.055f, 0.065f, 0.075f, 0.94f);
    [SerializeField] private Color _dialogueBorderColor = new Color(0.68f, 0.60f, 0.44f, 0.85f);

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

    private readonly List<AudioSource> _openingSoundSources = new List<AudioSource>();
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
    private Text _continueHint;
    private GameObject _dialoguePanel;
    private GameObject _continuePrompt;
    private RectTransform _continuePromptRect;
    private Image _continuePromptImage;
    private Vector2 _continuePromptRestPosition;
    private Color _continuePromptRestColor;
    private GameObject _exitChoice;
    private RectTransform _exitChoiceRect;
    private Image _exitChoiceBackground;
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
        ShowSubtitle("[무언가 부서지는 소리와 남자의 비명]");
        PlaySound(_screamClip);
        PlaySound(_jigeBreakClip);
        // 비명을 끝까지 들은 뒤부터 새 Enter 입력을 받는다.
        float screamDuration = _screamClip != null ? _screamClip.length : 0f;
        if (screamDuration > 0f)
            yield return new WaitForSecondsRealtime(screamDuration);
        yield return WaitForEnter();
        Phase = "What";
        ShowSubtitle("뭐야?");
        yield return WaitForEnter();
        // 선택지를 고르는 동안 하단에 기존 대사를 남겨 두고 Enter로 확정한다.
        Phase = "ExitChoice";
        _exitChoice.SetActive(true);
        AudioManager.Instance.PlayButtonHover();
        yield return WaitForEnter();
        _exitChoice.SetActive(false);
        ShowSubtitle("");
        AudioManager.Instance.PlayButtonClick();
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
        yield return ContinueAfterOpening();
    }

    private IEnumerator ContinueAfterOpening()
    {
        _openingSoundSources.Clear();
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
        yield return WaitForNextSceneExit();
    }

    private IEnumerator WaitForNextSceneExit()
    {
        if (_nextSceneExitPoint == null) yield break;
        if (string.IsNullOrWhiteSpace(_nextSceneName) || !Application.CanStreamedLevelBeLoaded(_nextSceneName))
        {
            Debug.LogError($"Prologue: 다음 씬 '{_nextSceneName}'을 Build Settings의 Scene List에 등록하세요.", this);
            yield break;
        }

        while (_nextSceneExitPoint != null)
        {
            bool playerCanExit = !PauseController.IsPaused && !_player.Health.IsDead &&
                (StageManager.Instance == null || !StageManager.Instance.IsRespawning);
            float distance = Vector2.Distance(_player.transform.position, _nextSceneExitPoint.position);
            if (playerCanExit && distance <= _nextSceneTriggerDistance) break;
            yield return null;
        }
        if (_nextSceneExitPoint == null) yield break;

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(_nextSceneName, LoadSceneMode.Single);
        if (loadOperation == null) yield break;
        loadOperation.allowSceneActivation = false;
        LockPlayer();
        Phase = "LeavingPrologue";
        yield return FadeTo(1f, _fadeToBlack);
        SceneArrivalFade.Prepare(_nextSceneRevealDuration);
        Time.timeScale = 1f;
        loadOperation.allowSceneActivation = true;
        yield return loadOperation;
    }

    private void OnDrawGizmosSelected()
    {
        if (_nextSceneExitPoint == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_nextSceneExitPoint.position, _nextSceneTriggerDistance);
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

    private void PlaySound(AudioClip clip, float gain = 1f)
    {
        if (clip == null) return;
        AudioSource source = AudioManager.Instance.PlaySfxTracked(clip, gain: gain);
        if (source != null) _openingSoundSources.Add(source);
    }

    private void StopOpeningSounds()
    {
        foreach (AudioSource source in _openingSoundSources)
            if (source != null) source.Stop();
        _openingSoundSources.Clear();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void SkipOpeningForTesting()
    {
        StopAllCoroutines();
        if (Phase == "Preparing")
        {
            _transitionDirection = _location2TransitionPoint.position.x >= _player.transform.position.x ? 1f : -1f;
            _player.RangedAttack?.CancelCharge();
            _player.StateMachine.ChangeState(_player.IdleState);
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
        }
        StopOpeningSounds();
        ShowSubtitle("");
        _exitChoice.SetActive(false);
        _continuePrompt.SetActive(false);
        SetContinueKeyPress(0f);
        _fade.alpha = 0f;
        _fade.blocksRaycasts = false;
        _input.ClearInputState();
        UnlockPlayer();
        RestoreGameplayUi();
        _camera.Reset(true, true, true);
        IsIntroComplete = true;
        StartCoroutine(ContinueAfterOpening());
    }
#endif

    private void ShowSubtitle(string text)
    {
        _subtitle.text = text;
        _dialoguePanel.SetActive(!string.IsNullOrEmpty(text));
    }

    private IEnumerator WaitForEnter()
    {
        SetContinueKeyPress(0f);
        _continuePrompt.SetActive(true);
        // Require a fresh press so a held Enter cannot skip several steps.
        while (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter))
            yield return null;
        yield return null;
        while (!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter))
            yield return null;
        yield return AnimateContinueKeyPress();
        _continuePrompt.SetActive(false);
        yield return null;
    }

    private IEnumerator AnimateContinueKeyPress()
    {
        SetContinueKeyPress(1f);
        yield return new WaitForSecondsRealtime(0.05f);

        float elapsed = 0f;
        const float releaseDuration = 0.09f;
        while (elapsed < releaseDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float amount = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / releaseDuration));
            SetContinueKeyPress(amount);
            yield return null;
        }
        SetContinueKeyPress(0f);
    }

    private void SetContinueKeyPress(float amount)
    {
        if (_continuePromptRect == null || _continuePromptImage == null) return;
        _continuePromptRect.localScale = Vector3.one * Mathf.Lerp(1f, 0.88f, amount);
        _continuePromptRect.anchoredPosition = _continuePromptRestPosition + Vector2.down * (4f * amount);
        var pressedColor = new Color(_continuePromptRestColor.r * 0.78f,
            _continuePromptRestColor.g * 0.78f, _continuePromptRestColor.b * 0.78f, _continuePromptRestColor.a);
        _continuePromptImage.color = Color.Lerp(_continuePromptRestColor, pressedColor, amount);
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool isOpening = Phase == "Preparing" || Phase == "Scream" || Phase == "What" ||
            Phase == "ExitChoice" || Phase == "DoorOpening";
        if (_ready && isOpening && (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0)))
        {
            SkipOpeningForTesting();
            return;
        }
#endif
        if (_exitChoice == null || !_exitChoice.activeSelf) return;
        bool hovered = RectTransformUtility.RectangleContainsScreenPoint(_exitChoiceRect, Input.mousePosition);
        if (_choicePanelSprite != null)
            _exitChoiceBackground.color = hovered ? new Color(1f, 0.96f, 0.87f, 1f) : Color.white;
        else
            _exitChoiceBackground.color = hovered ? new Color(0.24f, 0.20f, 0.13f, 1f) : new Color(0.13f, 0.12f, 0.10f, 0.98f);
    }

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
        _dialoguePanel = new GameObject("DialoguePanel", typeof(RectTransform), typeof(Image), typeof(Outline));
        _dialoguePanel.transform.SetParent(subtitleCanvasObject.transform, false);
        RectTransform dialogueRect = _dialoguePanel.GetComponent<RectTransform>();
        dialogueRect.anchorMin = Vector2.zero;
        dialogueRect.anchorMax = new Vector2(1f, 0f);
        dialogueRect.pivot = new Vector2(0.5f, 0f);
        dialogueRect.offsetMin = new Vector2(_dialogueSideMargin, _dialogueBottomMargin);
        dialogueRect.offsetMax = new Vector2(-_dialogueSideMargin, _dialogueBottomMargin + _dialogueHeight);
        Image dialogueBackground = _dialoguePanel.GetComponent<Image>();
        ConfigureDialogueImage(dialogueBackground, _dialoguePanelSprite, _dialoguePanelColor);
        Outline dialogueOutline = _dialoguePanel.GetComponent<Outline>();
        dialogueOutline.effectColor = _dialogueBorderColor;
        dialogueOutline.effectDistance = new Vector2(1f, -1f);
        dialogueOutline.enabled = _dialoguePanelSprite == null;

        var subtitleObject = new GameObject("Subtitle", typeof(RectTransform), typeof(Text));
        subtitleObject.transform.SetParent(_dialoguePanel.transform, false);
        RectTransform subtitleRect = subtitleObject.GetComponent<RectTransform>();
        subtitleRect.anchorMin = Vector2.zero;
        subtitleRect.anchorMax = Vector2.one;
        subtitleRect.offsetMin = new Vector2(40f, 28f);
        subtitleRect.offsetMax = new Vector2(-40f, -28f);
        _subtitle = subtitleObject.GetComponent<Text>();
        _subtitle.font = _subtitleFont != null ? _subtitleFont :
            Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, _dialogueFontSize);
        _subtitle.fontSize = _dialogueFontSize;
        _subtitle.alignment = TextAnchor.MiddleCenter;
        _subtitle.color = _dialogueTextColor;
        _subtitle.raycastTarget = false;

        _continuePrompt = new GameObject("EnterKey", typeof(RectTransform), typeof(Image), typeof(Outline));
        _continuePrompt.transform.SetParent(_dialoguePanel.transform, false);
        RectTransform promptRect = _continuePrompt.GetComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(1f, 0f);
        promptRect.anchorMax = new Vector2(1f, 0f);
        promptRect.pivot = new Vector2(1f, 0f);
        promptRect.anchoredPosition = new Vector2(-32f, 16f);
        promptRect.sizeDelta = _enterKeySprite != null ? new Vector2(80f, 80f) : new Vector2(184f, 56f);
        Image keyBackground = _continuePrompt.GetComponent<Image>();
        ConfigureDialogueImage(keyBackground, _enterKeySprite, new Color(0.22f, 0.23f, 0.25f, 1f));
        keyBackground.type = Image.Type.Simple;
        keyBackground.preserveAspect = _enterKeySprite != null;
        _continuePromptRect = promptRect;
        _continuePromptImage = keyBackground;
        _continuePromptRestPosition = promptRect.anchoredPosition;
        _continuePromptRestColor = keyBackground.color;
        Outline keyOutline = _continuePrompt.GetComponent<Outline>();
        keyOutline.effectColor = new Color(0.50f, 0.51f, 0.53f, 1f);
        keyOutline.effectDistance = new Vector2(1f, -1f);
        keyOutline.enabled = _enterKeySprite == null;

        var keyFace = new GameObject("KeyFace", typeof(RectTransform), typeof(Image));
        keyFace.transform.SetParent(_continuePrompt.transform, false);
        RectTransform keyFaceRect = keyFace.GetComponent<RectTransform>();
        keyFaceRect.anchorMin = Vector2.zero;
        keyFaceRect.anchorMax = Vector2.one;
        keyFaceRect.offsetMin = new Vector2(3f, 6f);
        keyFaceRect.offsetMax = new Vector2(-3f, -3f);
        Image keyFaceImage = keyFace.GetComponent<Image>();
        keyFaceImage.color = new Color(0.09f, 0.10f, 0.12f, 1f);
        keyFaceImage.raycastTarget = false;

        var hintObject = new GameObject("ContinueHint", typeof(RectTransform), typeof(Text));
        hintObject.transform.SetParent(keyFace.transform, false);
        RectTransform hintRect = hintObject.GetComponent<RectTransform>();
        hintRect.anchorMin = Vector2.zero;
        hintRect.anchorMax = Vector2.one;
        hintRect.offsetMin = new Vector2(8f, 0f);
        hintRect.offsetMax = new Vector2(-65f, 0f);
        _continueHint = hintObject.GetComponent<Text>();
        _continueHint.font = _subtitle.font;
        _continueHint.fontSize = 26;
        _continueHint.alignment = TextAnchor.MiddleCenter;
        _continueHint.color = new Color(0.94f, 0.94f, 0.94f, 1f);
        _continueHint.text = "Enter";
        _continueHint.raycastTarget = false;

        // 화살표는 글꼴의 특수문자 지원 여부에 관계없이 같은 모양으로 표시한다.
        var returnArrow = new GameObject("ReturnArrow", typeof(RectTransform));
        returnArrow.transform.SetParent(keyFace.transform, false);
        RectTransform arrowRect = returnArrow.GetComponent<RectTransform>();
        arrowRect.anchorMin = new Vector2(1f, 0.5f);
        arrowRect.anchorMax = new Vector2(1f, 0.5f);
        arrowRect.pivot = new Vector2(1f, 0.5f);
        arrowRect.anchoredPosition = new Vector2(-16f, 0f);
        arrowRect.sizeDelta = new Vector2(50f, 32f);
        CreateKeyStroke(arrowRect, "Horizontal", new Vector2(-1f, -4f), new Vector2(42f, 3f));
        CreateKeyStroke(arrowRect, "Vertical", new Vector2(20f, 5f), new Vector2(3f, 18f));
        CreateKeyStroke(arrowRect, "ArrowUpper", new Vector2(-17f, 1f), new Vector2(14f, 3f), 45f);
        CreateKeyStroke(arrowRect, "ArrowLower", new Vector2(-17f, -9f), new Vector2(14f, 3f), -45f);
        keyFace.SetActive(_enterKeySprite == null);
        _continuePrompt.SetActive(false);

        _exitChoice = new GameObject("ExitChoice", typeof(RectTransform), typeof(Image), typeof(Outline));
        _exitChoice.transform.SetParent(_dialoguePanel.transform, false);
        _exitChoiceRect = _exitChoice.GetComponent<RectTransform>();
        _exitChoiceRect.anchorMin = Vector2.one;
        _exitChoiceRect.anchorMax = Vector2.one;
        _exitChoiceRect.pivot = new Vector2(1f, 0f);
        _exitChoiceRect.anchoredPosition = new Vector2(0f, 18f);
        _exitChoiceRect.sizeDelta = new Vector2(360f, 112f);
        _exitChoiceBackground = _exitChoice.GetComponent<Image>();
        ConfigureDialogueImage(_exitChoiceBackground, _choicePanelSprite, new Color(0.13f, 0.12f, 0.10f, 0.98f));
        _exitChoiceBackground.pixelsPerUnitMultiplier = 2f;
        Outline choiceOutline = _exitChoice.GetComponent<Outline>();
        choiceOutline.effectColor = _dialogueBorderColor;
        choiceOutline.effectDistance = new Vector2(1f, -1f);
        choiceOutline.enabled = _choicePanelSprite == null;
        var choiceLabelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        choiceLabelObject.transform.SetParent(_exitChoice.transform, false);
        RectTransform choiceLabelRect = choiceLabelObject.GetComponent<RectTransform>();
        choiceLabelRect.anchorMin = Vector2.zero;
        choiceLabelRect.anchorMax = Vector2.one;
        choiceLabelRect.offsetMin = new Vector2(40f, 16f);
        choiceLabelRect.offsetMax = new Vector2(-40f, -16f);
        Text choiceLabel = choiceLabelObject.GetComponent<Text>();
        choiceLabel.font = _subtitle.font;
        choiceLabel.fontSize = 32;
        choiceLabel.alignment = TextAnchor.MiddleCenter;
        choiceLabel.color = _choiceTextColor;
        choiceLabel.text = "나가보자.";
        choiceLabel.raycastTarget = false;

        var choiceMarkerObject = new GameObject("SelectionArrow", typeof(RectTransform), typeof(Text));
        choiceMarkerObject.transform.SetParent(_exitChoice.transform, false);
        RectTransform choiceMarkerRect = choiceMarkerObject.GetComponent<RectTransform>();
        choiceMarkerRect.anchorMin = new Vector2(1f, 0f);
        choiceMarkerRect.anchorMax = Vector2.one;
        choiceMarkerRect.pivot = new Vector2(1f, 0.5f);
        choiceMarkerRect.sizeDelta = new Vector2(56f, 0f);
        choiceMarkerRect.anchoredPosition = new Vector2(-12f, 0f);
        Text choiceMarker = choiceMarkerObject.GetComponent<Text>();
        choiceMarker.font = _subtitle.font;
        choiceMarker.fontSize = 42;
        choiceMarker.alignment = TextAnchor.MiddleCenter;
        choiceMarker.color = choiceLabel.color;
        choiceMarker.text = "‹";
        choiceMarker.raycastTarget = false;
        _exitChoice.SetActive(false);
        _dialoguePanel.SetActive(false);
    }

    private static void ConfigureDialogueImage(Image image, Sprite sprite, Color fallbackColor)
    {
        image.sprite = sprite;
        image.color = sprite != null ? Color.white : fallbackColor;
        image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.raycastTarget = false;
    }

    private static void CreateKeyStroke(RectTransform parent, string name, Vector2 position, Vector2 size, float angle = 0f)
    {
        var stroke = new GameObject(name, typeof(RectTransform), typeof(Image));
        stroke.transform.SetParent(parent, false);
        RectTransform rect = stroke.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        Image image = stroke.GetComponent<Image>();
        image.color = new Color(0.94f, 0.94f, 0.94f, 1f);
        image.raycastTarget = false;
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
        StopOpeningSounds();
        if (_cinematics != null)
        {
            _cinematics.OnCinematicFinished.RemoveListener(HandleCinematicFinished);
            _cinematics.OnCinematicTargetReached.RemoveListener(HandleJigeReached);
            if (_locked && _cinematics.IsPlaying) _cinematics.Stop();
        }
        UnlockPlayer();
        RestoreGameplayUi();
        if (_subtitle != null) ShowSubtitle("");
        SetContinueKeyPress(0f);
        if (_continuePrompt != null) _continuePrompt.SetActive(false);
        if (_exitChoice != null) _exitChoice.SetActive(false);
        if (_fade != null)
        {
            _fade.alpha = 0f;
            _fade.blocksRaycasts = false;
        }
    }
}
