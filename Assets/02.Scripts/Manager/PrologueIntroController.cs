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

    [Header("Location 2 camera boundaries")]
    [SerializeField] private bool _useLocation2Boundaries = true;
    [Tooltip("화면 가장자리의 월드 좌표입니다. 카메라 중심 좌표가 아닙니다.")]
    [SerializeField] private float _location2LeftBoundary = 185f;
    [SerializeField] private float _location2RightBoundary = 230f;
    [SerializeField] private float _location2TopBoundary = 15f;
    [SerializeField] private float _location2BottomBoundary = -10f;

    [Header("Next scene exit")]
    [Tooltip("배경 2로 이동한 뒤 이 오브젝트에 가까워지면 다음 씬으로 넘어갑니다. 비워두면 전환하지 않습니다.")]
    [SerializeField] private Transform _nextSceneExitPoint;
    [SerializeField] private string _nextSceneName = "Test_Ep1";
    [Tooltip("출구 지점과 플레이어 사이의 거리입니다. X/Y 거리만 사용합니다.")]
    [SerializeField, Min(0.1f)] private float _nextSceneTriggerDistance = 1.5f;
    [Tooltip("다음 씬을 불러온 뒤 검은 화면이 밝아지는 시간입니다.")]
    [SerializeField, Min(0.01f)] private float _nextSceneRevealDuration = 0.75f;

    [Header("Movement and jump tutorial")]
    [Tooltip("이동 키를 누른 채 실제로 움직인 거리(누적)가 이 값 이상이면 이동을 익힌 것으로 판정")]
    [SerializeField, Min(0.5f)] private float _moveLearnDistance = 3f;
    [Tooltip("동작을 완료한 뒤 안내를 더 보여주는 시간 (초). 항상 같은 시간 뒤에 사라짐")]
    [SerializeField, Min(0f)] private float _controlHintLinger = 0.6f;
    [Tooltip("안내가 나타나고 사라질 때 페이드 시간 (초)")]
    [SerializeField, Min(0.01f)] private float _controlHintFadeDuration = 0.25f;
    [SerializeField] private Vector2 _controlHintOffset = new Vector2(0f, 64f);
    [SerializeField] private Font _controlHintFont;
    [SerializeField] private Sprite _moveLeftKeySprite;
    [SerializeField] private Sprite _moveRightKeySprite;
    [SerializeField] private Sprite _jumpCKeySprite;
    [SerializeField] private Sprite _jumpSpaceKeySprite;
    [SerializeField] private Transform _jumpObstacle;
    [SerializeField, Min(0.1f)] private float _jumpHintDistance = 4f;
    [SerializeField, TextArea] private string _moveHintText = "이동하기";
    [SerializeField, TextArea] private string _jumpHintText = "점프";
    private RectTransform _controlHintRoot;
    private GameObject _moveHintRow;
    private GameObject _jumpHintRow;
    private CanvasGroup _controlHintGroup;
    private GameObject _visibleHintRow;
    private float _controlHintAlpha;
    private float _controlHintTime;
    private bool _movementStarted;
    private bool _movementLearned;
    private float _lastPlayerX;
    private float _movedDistance;
    private bool _jumpHintStarted;
    private bool _jumpAttempted;
    private bool _jumpLearned;
    private float _movementDoneAt = -1f;
    private float _jumpDoneAt = -1f;

    [Header("Notice board and wanted poster")]
    [SerializeField] private Transform _noticeBoard;
    [SerializeField] private Transform _noticeBoardTrigger;
    [Tooltip("다른 브랜치에서 가져온 현상수배 Sprite를 여기에 연결하세요.")]
    [SerializeField] private Sprite _wantedPosterSprite;
    [SerializeField, Min(0.1f)] private float _noticeBoardReadDistance = 2.5f;
    [SerializeField, Min(1f)] private float _noticeBoardZoom = 1.6f;
    [SerializeField, TextArea] private string _noticeBoardText = "게시판에 무언가가 붙어있다. 확인해보자.";
    [SerializeField, TextArea] private string _afterPosterText = "잡으러 가자.";
    [SerializeField, TextArea] private string _jigeStoryText = "요괴들이 지게꾼을 괴롭혔나 보네.";
    private Transform _noticeBoardFocus;
    private GameObject _wantedPosterRoot;
    private Image _wantedPosterImage;
    private GameObject _wantedPosterPrompt;
    private bool _posterRead;
    private bool _noticeBoardIntroduced;
    private bool _posterBusy;
    private RectTransform _boardKeyHint;

    [Header("Broken jige inspection")]
    [SerializeField] private Transform _jige;
    [SerializeField, Min(0.1f)] private float _jigeTriggerDistance = 3.5f;
    [SerializeField, Min(1f)] private float _jigeZoom = 2f;
    [SerializeField, Min(0.1f)] private float _jigePanDuration = 0.8f;

    [Header("Dialogue")]
    [SerializeField, TextArea] private string _openingSoundText = "[무언가 부서지는 소리와 남자의 비명]";
    [SerializeField, TextArea] private string _openingReactionText = "뭐야?";
    [SerializeField, TextArea] private string _exitChoiceText = "나가보자.";
    [SerializeField] private Font _subtitleFont;

    [Header("Dialogue images (optional)")]
    [Tooltip("하단 대사창의 배경 그림입니다. 비워두면 기본 디자인을 사용합니다.")]
    [SerializeField] private Sprite _dialoguePanelSprite;
    [Tooltip("오른쪽 위 선택지의 배경 그림입니다. 선택지 글자는 별도로 표시합니다.")]
    [SerializeField] private Sprite _choicePanelSprite;
    [Tooltip("대사를 넘기고 게시판을 여는 Enter 키 이미지입니다.")]
    [SerializeField] private Sprite _advanceKeySprite;

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

    private readonly List<GameAudio.PlayHandle> _openingSoundSources = new List<GameAudio.PlayHandle>();
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
    private bool _isLeavingPrologue;
    private bool _hasWarnedInvalidExit;
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

    private void OnEnable()
    {
        Canvas.preWillRenderCanvases += UpdateHintsBeforeRender;
        if (!_ready) return;
        BindCinematicCallbacks();
        if (IsIntroComplete && Phase != "LeavingPrologue")
            StartCoroutine(ResumeProgression());
    }

    private void BindCinematicCallbacks()
    {
        _cinematics.OnCinematicFinished.RemoveListener(HandleCinematicFinished);
        _cinematics.OnCinematicTargetReached.RemoveListener(HandleJigeReached);
        _cinematics.OnCinematicFinished.AddListener(HandleCinematicFinished);
        _cinematics.OnCinematicTargetReached.AddListener(HandleJigeReached);
    }

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

        CacheDestinationLayout();

        _inputWasEnabled = _input.enabled;
        _playerWasEnabled = _player.enabled;
        _bodyWasSimulated = _body.simulated;
        _input.ClearInputState();
        _input.enabled = false;
        _locked = true;
        Phase = "Preparing";
        BindCinematicCallbacks();

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
        _noticeBoardFocus = new GameObject("NoticeBoardCameraFocus").transform;
        _noticeBoardFocus.SetParent(transform, false);
        _ready = true;
    }

    private void CacheDestinationLayout()
    {
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
    }

    private IEnumerator ResumeProgression()
    {
        yield return null;
        if (IsTransitionComplete || _background2.activeSelf)
        {
            Phase = "Complete";
            yield break;
        }

        // Finish any camera return after rebinding before restarting the interrupted progression.
        while (_cinematics.IsPlaying) yield return null;
        if (_background2Layout.Count == 0) CacheDestinationLayout();
        if (Mathf.Approximately(_transitionDirection, 0f))
        {
            Transform origin = _noticeBoard != null ? _noticeBoard : _jige;
            _transitionDirection = _location2TransitionPoint.position.x >= origin.position.x ? 1f : -1f;
        }
        _posterBusy = false;
        if (_wantedPosterRoot != null) _wantedPosterRoot.SetActive(false);
        if (_continuePrompt != null) _continuePrompt.SetActive(false);
        ShowSubtitle("");
        _fade.alpha = 0f;
        _fade.blocksRaycasts = false;
        RestoreGameplayUi();
        yield return ContinueAfterOpening();
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
        ShowSubtitle(_openingSoundText);
        PlaySound("Prologue.Scream");
        PlaySound("Prologue.Break");
        // 비명 도중에도 Enter로 넘길 수 있음. 넘기면 비명 / 부서지는 소리를 바로 끊음
        yield return WaitForAdvance();
        StopOpeningSounds();
        Phase = "What";
        ShowSubtitle(_openingReactionText);
        yield return WaitForAdvance();
        // 선택지를 고르는 동안 하단에 기존 대사를 남겨 두고 Enter로 확정한다.
        Phase = "ExitChoice";
        _exitChoice.SetActive(true);
        AudioManager.Instance.PlayButtonHover();
        yield return WaitForAdvance();
        _exitChoice.SetActive(false);
        ShowSubtitle("");
        AudioManager.Instance.PlayButtonClick();
        Phase = "DoorOpening";
        PlaySound("Prologue.Door");
        // Keep the screen black until the last second of the door sound.
        float doorRevealDelay = Mathf.Max(0f, GetSoundDuration("Prologue.Door") - 1f);
        if (doorRevealDelay > 0f)
            yield return new WaitForSecondsRealtime(doorRevealDelay);
        // Player appears outside the house and can move as soon as the reveal starts.
        AudioManager.Instance?.PlayMusicEvent("Music.Prologue", gameObject.scene);
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
        yield return ReadNoticeBoard();
        if (!IsJigeInspected)
        {
            // Use the route direction even if progression resumes after passing the jige.
            while (_posterBusy ||
                   (_player.transform.position.x - (_jige.position.x - _jigeTriggerDistance * _transitionDirection)) * _transitionDirection < 0f)
            {
                yield return null;
            }

            ShowSubtitle("");
            LockPlayer();
            Phase = "Inspecting";
            _cinematicFinished = false;
            _jigeReached = false;
            _jigeFocus.position = _jige.position;
            SpriteRenderer jigeRenderer = _jige.GetComponentInChildren<SpriteRenderer>();
            if (jigeRenderer != null) _jigeFocus.position = jigeRenderer.bounds.center;
            _cinematics.CinematicTargets = new List<CinematicTarget> {
                new CinematicTarget { TargetTransform = _jigeFocus, EaseInDuration = _jigePanDuration,
                    HoldDuration = -1f, Zoom = _jigeZoom, EaseType = EaseType.EaseInOut }
            };
            _cinematics.Play();
            while (!_jigeReached) yield return null;
            Phase = "JigeDialogue";
            ShowSubtitle(_jigeStoryText);
            yield return WaitForAdvance();
            ShowSubtitle("");
            _cinematics.Stop();
            Phase = "ReturningToPlayer";
            while (!_cinematicFinished) yield return null;
            UnlockPlayer();
            IsJigeInspected = true;
            _fade.blocksRaycasts = false;
            RestoreGameplayUi();
        }
        Phase = "GameplayAfterJige";
        while (_posterBusy ||
               (_player.transform.position.x - _location2TransitionPoint.position.x) * _transitionDirection < 0f)
        {
            yield return null;
        }

        LockPlayer();
        Phase = "FadeToBlack";
        yield return FadeTo(1f, _fadeToBlack);

        // Swap only while completely covered. Keep the player's authored Z depth.
        Phase = "Switching";
        TransferToLocation2();

        // Allow new renderers and camera extensions to update beneath the black screen.
        yield return null;
        Phase = "Revealing";
        yield return FadeTo(0f, _revealLocation2);
        UnlockPlayer();
        _fade.blocksRaycasts = false;
        RestoreGameplayUi();
        Phase = "Complete";
    }

    private void TransferToLocation2()
    {
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

        ApplyLocation2Boundaries();

        // Reset extensions first, then place the camera inside the destination limits.
        _camera.Reset(false, true, true);
        CenterLocation2Camera();
        foreach (KeyValuePair<Transform, Vector3> entry in _background2Layout)
            if (entry.Key != null) entry.Key.localPosition = entry.Value;
        SetActive(_background2, true);
        SetActive(_environment2, true);
        if (_alignBackground2ToCamera) AlignDestinationBackground();
        foreach (ParallaxManager parallax in _destinationParallax)
            parallax.RebaseAtCurrentPosition();

    }

    private void CenterLocation2Camera()
    {
        _camera.CenterOnTargets();
        Vector3 position = _camera.transform.localPosition;
        ProCamera2DNumericBoundaries boundaries = _camera.GetComponent<ProCamera2DNumericBoundaries>();
        if (boundaries != null && boundaries.enabled && boundaries.UseNumericBoundaries)
        {
            Vector2 halfSize = _camera.ScreenSizeInWorldCoordinates * 0.5f;
            if (boundaries.UseLeftBoundary) position.x = Mathf.Max(position.x, boundaries.LeftBoundary + halfSize.x);
            if (boundaries.UseRightBoundary) position.x = Mathf.Min(position.x, boundaries.RightBoundary - halfSize.x);
            if (boundaries.UseBottomBoundary) position.y = Mathf.Max(position.y, boundaries.BottomBoundary + halfSize.y);
            if (boundaries.UseTopBoundary) position.y = Mathf.Min(position.y, boundaries.TopBoundary - halfSize.y);
        }
        _camera.MoveCameraInstantlyToPosition(new Vector2(position.x, position.y));
    }

    private void ApplyLocation2Boundaries()
    {
        if (!_useLocation2Boundaries) return;
        ProCamera2DNumericBoundaries boundaries = _camera.GetComponent<ProCamera2DNumericBoundaries>();
        if (boundaries == null) boundaries = _camera.gameObject.AddComponent<ProCamera2DNumericBoundaries>();
        Vector3 parentPosition = _camera.transform.parent != null
            ? _camera.transform.parent.position : Vector3.zero;
        boundaries.enabled = true;
        boundaries.UseNumericBoundaries = true;
        boundaries.UseLeftBoundary = true;
        boundaries.UseRightBoundary = true;
        boundaries.UseTopBoundary = true;
        boundaries.UseBottomBoundary = true;
        boundaries.LeftBoundary = Mathf.Min(_location2LeftBoundary, _location2RightBoundary) - parentPosition.x;
        boundaries.RightBoundary = Mathf.Max(_location2LeftBoundary, _location2RightBoundary) - parentPosition.x;
        boundaries.BottomBoundary = Mathf.Min(_location2BottomBoundary, _location2TopBoundary) - parentPosition.y;
        boundaries.TopBoundary = Mathf.Max(_location2BottomBoundary, _location2TopBoundary) - parentPosition.y;
        boundaries.TargetLeftBoundary = boundaries.LeftBoundary;
        boundaries.TargetRightBoundary = boundaries.RightBoundary;
        boundaries.TargetBottomBoundary = boundaries.BottomBoundary;
        boundaries.TargetTopBoundary = boundaries.TopBoundary;
    }

    private void UpdateNextSceneExit()
    {
        if (!_ready || !IsIntroComplete || !_background2.activeSelf || _isLeavingPrologue ||
            _nextSceneExitPoint == null || PauseController.IsPaused || _player.Health.IsDead ||
            (StageManager.Instance != null && StageManager.Instance.IsRespawning))
        {
            return;
        }

        if (Vector2.Distance(_player.transform.position, _nextSceneExitPoint.position) > _nextSceneTriggerDistance)
            return;
        if (string.IsNullOrWhiteSpace(_nextSceneName) || !Application.CanStreamedLevelBeLoaded(_nextSceneName))
        {
            if (!_hasWarnedInvalidExit)
            {
                Debug.LogError($"Prologue: 다음 씬 '{_nextSceneName}'을 Build Settings의 Scene List에 등록하세요.", this);
                _hasWarnedInvalidExit = true;
            }
            return;
        }

        _isLeavingPrologue = true;
        StopAllCoroutines();
        StartCoroutine(TransitionToNextScene());
    }

    private IEnumerator TransitionToNextScene()
    {
        Phase = "LeavingPrologue";
        Ep0CombatTutorial tutorial = FindFirstObjectByType<Ep0CombatTutorial>();
        if (tutorial != null && tutorial.gameObject.scene == gameObject.scene)
            tutorial.CancelForSceneExit();
        StopOpeningSounds();
        _cinematics.Stop();
        UnlockPlayer();
        _posterBusy = false;
        if (_wantedPosterRoot != null) _wantedPosterRoot.SetActive(false);
        if (_boardKeyHint != null) _boardKeyHint.gameObject.SetActive(false);
        if (_controlHintRoot != null) _controlHintRoot.gameObject.SetActive(false);
        _exitChoice.SetActive(false);
        _continuePrompt.SetActive(false);
        HitStop.Stop();
        CameraShake.Stop();
        LockPlayer();
        ShowSubtitle("");
        // Render a fully black, persistent overlay before any scene loading work.
        yield return SceneArrivalFade.FadeOut(_fadeToBlack);
        SceneArrivalFade.Prepare(_nextSceneRevealDuration);
        yield return new WaitForEndOfFrame();
        yield return null;
        Time.timeScale = 1f;
        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(_nextSceneName, LoadSceneMode.Single);
        if (loadOperation == null)
        {
            SceneArrivalFade.Cancel();
            UnlockPlayer();
            RestoreGameplayUi();
            Phase = "Complete";
            _isLeavingPrologue = false;
            yield break;
        }
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

    private float GetSoundDuration(string eventId)
    {
        AudioManager manager = AudioManager.Instance;
        GameAudio.SoundSetSO set = manager != null ? manager.Catalog?.Prologue : null;
        if (set == null || !set.TryGetEvent(eventId, out GameAudio.EventBinding binding) || !binding.Enabled ||
            !set.TryGetSound(binding.SoundId, out GameAudio.SoundData sound) || !sound.HasValidClip)
        {
            return 0f;
        }

        float duration = 0f;
        foreach (AudioClip clip in sound.Clips)
        {
            if (sound.TryGetStartSample(clip, out int startSample))
                duration = Mathf.Max(duration, (clip.samples - startSample) / (float)clip.frequency / Mathf.Max(0.01f, sound.PitchMin));
        }
        return binding.EffectiveDelay + duration;
    }

    private void PlaySound(string eventId)
    {
        var emitter = GameAudio.EventAudio.For(this, "Prologue");
        GameAudio.PlayHandle handle = emitter.Play(eventId);
        if (!handle.IsEmpty) _openingSoundSources.Add(handle);
    }

    private void StopOpeningSounds()
    {
        AudioManager manager = AudioManager.ExistingInstance;
        if (manager != null)
        {
            foreach (GameAudio.PlayHandle handle in _openingSoundSources) manager.StopHandle(handle);
        }
        if (TryGetComponent(out GameAudio.EventAudio emitter)) emitter.Cancel();
        _openingSoundSources.Clear();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void SkipToLocation2ForTesting()
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
        _posterBusy = false;
        if (_wantedPosterRoot != null) _wantedPosterRoot.SetActive(false);
        ShowSubtitle("");
        _exitChoice.SetActive(false);
        _continuePrompt.SetActive(false);
        SetContinueKeyPress(0f);
        _fade.alpha = 0f;
        _fade.blocksRaycasts = false;
        _input.ClearInputState();
        UnlockPlayer();
        RestoreGameplayUi();
        _cinematics.Stop();
        _player.RangedAttack?.CancelCharge();
        _player.StateMachine.ChangeState(_player.IdleState);
        TransferToLocation2();
        IsIntroComplete = true;
        IsJigeInspected = true;
        Phase = "Complete";
        _isLeavingPrologue = false;
    }
#endif

    private void ShowSubtitle(string text)
    {
        _subtitle.text = text;
        bool visible = !string.IsNullOrEmpty(text);
        _dialoguePanel.SetActive(visible);
        _continuePrompt.SetActive(visible);
    }

    private IEnumerator WaitForAdvance(GameObject prompt = null)
    {
        SetContinueKeyPress(0f);
        GameObject activePrompt = prompt != null ? prompt : _continuePrompt;
        activePrompt.SetActive(true);
        // Require a fresh Enter press so opening a poster cannot also close it.
        while ((Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter)))
            yield return null;
        yield return null;
        while (!(Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            yield return null;
        if (prompt == null) yield return AnimateContinueKeyPress();
        activePrompt.SetActive(false);
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
        UpdateNextSceneExit();
        if (_isLeavingPrologue) return;
        UpdateControlHints();
        UpdatePosterReopen();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_ready && Phase != "Complete" &&
            (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0)))
        {
            SkipToLocation2ForTesting();
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

    private IEnumerator ReadNoticeBoard()
    {
        if (_posterRead || _noticeBoardIntroduced || _noticeBoard == null || _noticeBoardTrigger == null) yield break;
        float direction = _transitionDirection;
        while ((_player.transform.position.x - _noticeBoardTrigger.position.x) * direction < 0f)
            yield return null;

        LockPlayer();
        Phase = "NoticeBoardFocus";
        _noticeBoardFocus.position = _noticeBoard.position;
        SpriteRenderer renderer = _noticeBoard.GetComponentInChildren<SpriteRenderer>();
        if (renderer != null) _noticeBoardFocus.position = renderer.bounds.center;
        _cinematicFinished = false;
        _jigeReached = false;
        _cinematics.UseLetterbox = true;
        _cinematics.CinematicTargets = new List<CinematicTarget>
        {
            new CinematicTarget
            {
                TargetTransform = _noticeBoardFocus,
                EaseInDuration = _jigePanDuration,
                HoldDuration = -1f,
                Zoom = _noticeBoardZoom,
                EaseType = EaseType.EaseInOut
            }
        };
        _cinematics.Play();
        while (!_jigeReached) yield return null;
        ShowSubtitle(_noticeBoardText);
        yield return WaitForAdvance();
        ShowSubtitle("");
        _cinematics.Stop();
        while (!_cinematicFinished) yield return null;
        UnlockPlayer();
        _fade.blocksRaycasts = false;
        RestoreGameplayUi();
        _noticeBoardIntroduced = true;
        Phase = "Gameplay";
    }

    private bool IsNearNoticeBoard()
    {
        return _noticeBoard != null &&
            Mathf.Abs(_player.transform.position.x - _noticeBoard.position.x) <= _noticeBoardReadDistance;
    }

    private IEnumerator ReadWantedPoster(bool firstRead)
    {
        _posterBusy = true;
        LockPlayer();
        if (_wantedPosterRoot == null) CreateWantedPoster();
        _wantedPosterImage.sprite = _wantedPosterSprite;
        _wantedPosterImage.enabled = _wantedPosterSprite != null;
        _wantedPosterRoot.SetActive(true);
        ShowSubtitle("");
        if (_boardKeyHint != null) _boardKeyHint.gameObject.SetActive(false);
        yield return WaitForAdvance(_wantedPosterPrompt);
        _wantedPosterRoot.SetActive(false);
        _posterRead = true;
        if (firstRead)
        {
            ShowSubtitle(_afterPosterText);
            yield return WaitForAdvance();
        }
        ShowSubtitle("");
        UnlockPlayer();
        _fade.blocksRaycasts = false;
        RestoreGameplayUi();
        _posterBusy = false;
    }

    private void UpdatePosterReopen()
    {
        bool idle = Phase == "Gameplay" || Phase == "GameplayAfterJige";
        bool available = _ready && !_posterBusy && !PauseController.IsPaused &&
            !_locked && (_noticeBoardIntroduced || _posterRead) && idle && IsNearNoticeBoard();
        if (!available)
        {
            if (_boardKeyHint != null) _boardKeyHint.gameObject.SetActive(false);
            return;
        }

        if (_boardKeyHint == null) CreateBoardKeyHint();
        _boardKeyHint.gameObject.SetActive(true);
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            StartCoroutine(ReadWantedPoster(!_posterRead));
    }

    private void UpdateBoardHintPosition()
    {
        if (_boardKeyHint == null || !_boardKeyHint.gameObject.activeInHierarchy || _noticeBoard == null) return;
        SpriteRenderer renderer = _noticeBoard.GetComponentInChildren<SpriteRenderer>();
        Vector3 top = renderer != null
            ? new Vector3(renderer.bounds.center.x, renderer.bounds.max.y, _noticeBoard.position.z)
            : _noticeBoard.position + Vector3.up * 2f;
        TutorialHintPositioning.Position(_boardKeyHint, _camera.GetComponent<Camera>(), top, Vector2.up * 48f);
    }

    private void CreateBoardKeyHint()
    {
        var canvasObject = new GameObject("NoticeBoardKeyHint", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32765;
        canvas.pixelPerfect = true;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var key = new GameObject("Enter", typeof(RectTransform), typeof(Image));
        key.transform.SetParent(canvasObject.transform, false);
        _boardKeyHint = key.GetComponent<RectTransform>();
        _boardKeyHint.anchorMin = new Vector2(0.5f, 0.5f);
        _boardKeyHint.anchorMax = _boardKeyHint.anchorMin;
        _boardKeyHint.sizeDelta = new Vector2(64f, 64f);
        Image image = key.GetComponent<Image>();
        image.sprite = _advanceKeySprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private void CreateWantedPoster()
    {
        _wantedPosterRoot = new GameObject("WantedPoster", typeof(RectTransform));
        _wantedPosterRoot.transform.SetParent(_dialoguePanel.transform.parent, false);
        RectTransform root = _wantedPosterRoot.GetComponent<RectTransform>();
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = root.anchorMin;
        root.anchoredPosition = Vector2.zero;
        root.sizeDelta = new Vector2(1200f, 700f);
        var paper = new GameObject("PosterImage", typeof(RectTransform), typeof(Image));
        paper.transform.SetParent(root, false);
        RectTransform imageRect = paper.GetComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;
        _wantedPosterImage = paper.GetComponent<Image>();
        _wantedPosterImage.preserveAspect = true;
        _wantedPosterImage.raycastTarget = false;
        _wantedPosterPrompt = new GameObject("Enter", typeof(RectTransform), typeof(Image));
        _wantedPosterPrompt.transform.SetParent(root, false);
        RectTransform promptRect = _wantedPosterPrompt.GetComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(1f, 0f);
        promptRect.anchorMax = promptRect.anchorMin;
        promptRect.pivot = new Vector2(1f, 0f);
        promptRect.anchoredPosition = new Vector2(-24f, 24f);
        promptRect.sizeDelta = new Vector2(120f, 72f);
        Image promptImage = _wantedPosterPrompt.GetComponent<Image>();
        promptImage.sprite = _advanceKeySprite;
        promptImage.preserveAspect = true;
        promptImage.raycastTarget = false;
        _wantedPosterRoot.SetActive(false);
    }

    private void UpdateControlHints()
    {
        bool active = _ready && IsIntroComplete && !_locked &&
            (Phase == "Gameplay" || Phase == "GameplayAfterJige") && !PauseController.IsPaused;
        if (!active)
        {
            if (_controlHintRoot != null) _controlHintRoot.gameObject.SetActive(false);
            return;
        }

        if (_controlHintRoot == null) CreateControlHints();
        _controlHintTime += Time.deltaTime;
        float playerX = _player.transform.position.x;
        if (!_movementStarted)
        {
            _movementStarted = true;
            _lastPlayerX = playerX;
        }

        // 이동: 이동 키를 누른 채 실제로 움직인 거리만 누적 (가만히 서 있던 시간, 밀려난 거리는 제외).
        // 다 움직이면 항상 같은 시간(_controlHintLinger) 뒤에 사라짐
        if (!_movementLearned)
        {
            if (Mathf.Abs(_input.MoveInputX) > 0.01f) _movedDistance += Mathf.Abs(playerX - _lastPlayerX);
            if (_movementDoneAt < 0f && _movedDistance >= _moveLearnDistance) _movementDoneAt = _controlHintTime;
            if (_movementDoneAt >= 0f && _controlHintTime - _movementDoneAt >= _controlHintLinger) _movementLearned = true;
        }
        _lastPlayerX = playerX;

        bool showJump = false;
        if (_jumpObstacle != null && !_jumpLearned)
        {
            Collider2D obstacle = _jumpObstacle.GetComponent<Collider2D>();
            float edge = obstacle != null ? obstacle.bounds.max.x : _jumpObstacle.position.x;
            float distance = _jumpObstacle.position.x - playerX;
            if (!_jumpHintStarted && distance >= -0.5f && distance <= _jumpHintDistance)
            {
                _jumpHintStarted = true;
                // 장애물까지 걸어왔으면 이동은 익힌 것. 점프 뒤에 이동 안내가 다시 나오지 않게 함
                _movementLearned = true;
            }
            if (_jumpHintStarted && _player.StateMachine.CurrentState == _player.JumpState)
                _jumpAttempted = true;
            // 점프: 점프해서 장애물을 넘어 착지하면 완료, 같은 시간 뒤에 사라짐
            if (_jumpDoneAt < 0f && _jumpAttempted && _player.IsGrounded && playerX > edge + 0.2f)
                _jumpDoneAt = _controlHintTime;
            if (_jumpDoneAt >= 0f && _controlHintTime - _jumpDoneAt >= _controlHintLinger) _jumpLearned = true;
            showJump = _jumpHintStarted && !_jumpLearned;
        }

        GameObject desiredRow = showJump ? _jumpHintRow : !_movementLearned ? _moveHintRow : null;
        UpdateControlHintFade(desiredRow);
    }

    /// <summary>안내 줄 전환과 숨김을 페이드로 처리 (보이던 줄이 다 사라진 뒤 다음 줄이 나타남)</summary>
    private void UpdateControlHintFade(GameObject desiredRow)
    {
        float step = Time.deltaTime / _controlHintFadeDuration;
        if (_visibleHintRow != desiredRow)
        {
            _controlHintAlpha = Mathf.MoveTowards(_controlHintAlpha, 0f, step);
            if (_controlHintAlpha <= 0f) _visibleHintRow = desiredRow;
        }
        else if (_visibleHintRow != null)
        {
            _controlHintAlpha = Mathf.MoveTowards(_controlHintAlpha, 1f, step);
        }

        _moveHintRow.SetActive(_visibleHintRow == _moveHintRow);
        _jumpHintRow.SetActive(_visibleHintRow == _jumpHintRow);
        _controlHintGroup.alpha = _controlHintAlpha;
        _controlHintRoot.gameObject.SetActive(_visibleHintRow != null);
    }

    private void UpdateHintsBeforeRender()
    {
        if (!_ready || _camera == null) return;
        if (_controlHintRoot != null && _controlHintRoot.gameObject.activeInHierarchy && _player != null)
        {
            TutorialHintPositioning.Position(_controlHintRoot, _camera.GetComponent<Camera>(),
                TutorialHintPositioning.GetRenderedPlayerAnchor(_player, true), _controlHintOffset);
        }

        UpdateBoardHintPosition();
    }

    private void CreateControlHints()
    {
        var canvasObject = new GameObject("OpeningControlHints", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32765;
        canvas.pixelPerfect = true;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var hint = new GameObject("PlayerControlHint", typeof(RectTransform), typeof(CanvasGroup));
        hint.transform.SetParent(canvasObject.transform, false);
        _controlHintRoot = hint.GetComponent<RectTransform>();
        _controlHintGroup = hint.GetComponent<CanvasGroup>();
        _controlHintGroup.alpha = 0f;
        _controlHintGroup.interactable = false;
        _controlHintGroup.blocksRaycasts = false;
        _controlHintRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _controlHintRoot.anchorMax = _controlHintRoot.anchorMin;
        _controlHintRoot.sizeDelta = new Vector2(420f, 64f);
        _moveHintRow = new GameObject("Movement", typeof(RectTransform));
        _moveHintRow.transform.SetParent(hint.transform, false);
        _jumpHintRow = new GameObject("Jump", typeof(RectTransform));
        _jumpHintRow.transform.SetParent(hint.transform, false);
        AddControlHintText(_moveHintRow.transform, _moveHintText, -150f, 130f);
        AddControlHintKey(_moveHintRow.transform, _moveLeftKeySprite, -35f, 64f);
        AddControlHintKey(_moveHintRow.transform, _moveRightKeySprite, 35f, 64f);
        AddControlHintText(_jumpHintRow.transform, _jumpHintText, -150f, 130f);
        AddControlHintKey(_jumpHintRow.transform, _jumpCKeySprite, -35f, 64f);
        AddControlHintText(_jumpHintRow.transform, "또는", 30f, 60f);
        AddControlHintKey(_jumpHintRow.transform, _jumpSpaceKeySprite, 120f, 110f);
        CenterControlHintRow(_moveHintRow.transform);
        CenterControlHintRow(_jumpHintRow.transform);
    }

    private void CenterControlHintRow(Transform row)
    {
        if (row.childCount == 0) return;
        float left = float.PositiveInfinity;
        float right = float.NegativeInfinity;
        foreach (RectTransform child in row)
        {
            left = Mathf.Min(left, child.anchoredPosition.x - child.sizeDelta.x * child.pivot.x);
            right = Mathf.Max(right, child.anchoredPosition.x + child.sizeDelta.x * (1f - child.pivot.x));
        }
        float center = (left + right) * 0.5f;
        foreach (RectTransform child in row)
            child.anchoredPosition -= new Vector2(center, 0f);
    }

    private void AddControlHintText(Transform parent, string value, float x, float width)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var label = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(Outline));
        label.transform.SetParent(parent, false);
        RectTransform rect = label.GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(width, 64f);
        Text text = label.GetComponent<Text>();
        text.text = value;
        text.font = _controlHintFont != null ? _controlHintFont : _subtitle.font;
        text.fontSize = 24;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.black;
        text.raycastTarget = false;
        Outline outline = label.GetComponent<Outline>();
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(1f, -1f);
    }

    private void AddControlHintKey(Transform parent, Sprite sprite, float x, float width)
    {
        var key = new GameObject("Key", typeof(RectTransform), typeof(Image));
        key.transform.SetParent(parent, false);
        RectTransform rect = key.GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(width, 44f);
        Image image = key.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
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

        _continuePrompt = new GameObject("AdvanceKey", typeof(RectTransform), typeof(Image), typeof(Outline));
        _continuePrompt.transform.SetParent(_dialoguePanel.transform, false);
        RectTransform promptRect = _continuePrompt.GetComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(1f, 0f);
        promptRect.anchorMax = new Vector2(1f, 0f);
        promptRect.pivot = new Vector2(1f, 0f);
        promptRect.anchoredPosition = new Vector2(-32f, 16f);
        promptRect.sizeDelta = _advanceKeySprite != null ? new Vector2(80f, 80f) : new Vector2(184f, 56f);
        Image keyBackground = _continuePrompt.GetComponent<Image>();
        ConfigureDialogueImage(keyBackground, _advanceKeySprite, new Color(0.22f, 0.23f, 0.25f, 1f));
        keyBackground.type = Image.Type.Simple;
        keyBackground.preserveAspect = _advanceKeySprite != null;
        _continuePromptRect = promptRect;
        _continuePromptImage = keyBackground;
        _continuePromptRestPosition = promptRect.anchoredPosition;
        _continuePromptRestColor = keyBackground.color;
        Outline keyOutline = _continuePrompt.GetComponent<Outline>();
        keyOutline.effectColor = new Color(0.50f, 0.51f, 0.53f, 1f);
        keyOutline.effectDistance = new Vector2(1f, -1f);
        keyOutline.enabled = _advanceKeySprite == null;

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
        keyFace.SetActive(_advanceKeySprite == null);
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
        choiceLabel.text = _exitChoiceText;
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
        Canvas.preWillRenderCanvases -= UpdateHintsBeforeRender;
        StopAllCoroutines();
        _isLeavingPrologue = false;
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
