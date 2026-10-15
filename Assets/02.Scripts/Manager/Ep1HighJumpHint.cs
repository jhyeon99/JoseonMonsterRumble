using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class Ep1HighJumpHint : MonoBehaviour
{
    [SerializeField, TextArea] private string _message = "길게 눌러 더 높이 점프";
    [SerializeField] private bool _showJumpKeys = true;
    [SerializeField, Min(0.1f)] private float _displayDuration = 6f;
    [SerializeField] private Font _font;
    [SerializeField] private Sprite _cKey;
    [SerializeField] private Sprite _spaceKey;
    [SerializeField, Min(0.1f)] private float _triggerDistance = 3f;
    [SerializeField] private Vector2 _headOffset = new Vector2(0f, 95f);
    [Tooltip("힌트가 나타나고 사라질 때 페이드 시간 (초)")]
    [SerializeField, Min(0.01f)] private float _fadeDuration = 0.25f;
    private Ep1TutorialManager _tutorialManager;
    private Ep1TutorialHintType _hintType;
    private Text _messageText;
    private Text _separatorText;
    private RectTransform _cKeyRect;
    private RectTransform _spaceKeyRect;
    private int _presentationVersion = -1;
    private Player _player;
    private PlayerInputHandler _input;
    private Camera _camera;
    private RectTransform _hint;
    private RectTransform _canvas;
    private Canvas _hintCanvas;
    private bool _shown;
    private bool _jumpStarted;
    private bool _heldLongEnough;
    private float _displayElapsed;
    private CanvasGroup _hintGroup;
    private float _hintAlpha;
    private bool _hintVisible;
    private bool _finishing;
    private bool _destroyWhenHidden;
    private bool IsTalismanHint => _hintType == Ep1TutorialHintType.TalismanTap ||
        _hintType == Ep1TutorialHintType.TalismanCharge;
    private bool IsRequiredPracticeHint => _hintType == Ep1TutorialHintType.HighJump ||
        _hintType == Ep1TutorialHintType.Parry || _hintType == Ep1TutorialHintType.Guard ||
        _hintType == Ep1TutorialHintType.Finisher || _hintType == Ep1TutorialHintType.PotionUse || IsTalismanHint;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Ep1TutorialManager manager = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Ep1TutorialManager candidate = root.GetComponentInChildren<Ep1TutorialManager>();
            if (candidate != null && candidate.isActiveAndEnabled)
            {
                manager = candidate;
                break;
            }
        }
        if (scene.name != "Test_Ep1" && manager == null) return;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform point in root.GetComponentsInChildren<Transform>(true))
            {
                string pointName = point.name.ToLowerInvariant();
                string prefabName;
                Ep1TutorialHintType hintType;
                if (pointName == "highjumphintpoint")
                {
                    prefabName = "Ep1HighJumpHint";
                    hintType = Ep1TutorialHintType.HighJump;
                }
                else if (pointName == "respawnhint")
                {
                    prefabName = "Ep1RespawnHint";
                    hintType = Ep1TutorialHintType.Respawn;
                }
                else if (pointName == "parryhint" || pointName == "parryhitn")
                {
                    prefabName = "Ep1ParryHint";
                    hintType = Ep1TutorialHintType.Parry;
                }
                else
                {
                    continue;
                }
                Ep1HighJumpHint hint = point.GetComponentInChildren<Ep1HighJumpHint>(true);
                if (manager != null && hintType == Ep1TutorialHintType.Respawn)
                {
                    if (hint != null) hint.Configure(manager, hintType);
                    continue;
                }
                if (hint == null)
                {
                    GameObject prefab = Resources.Load<GameObject>(prefabName);
                    if (prefab == null) continue;
                    hint = Instantiate(prefab, point, false).GetComponent<Ep1HighJumpHint>();
                }
                if (hint != null) hint.Configure(manager, hintType);
            }
        }
    }

    internal void Configure(Ep1TutorialManager manager, Ep1TutorialHintType hintType)
    {
        _tutorialManager = manager;
        _hintType = hintType;
        _presentationVersion = -1;
        if (_messageText != null) ApplyPresentation();
    }

    private void OnEnable()
    {
        Canvas.preWillRenderCanvases += UpdateHintPosition;
    }

    private void OnDisable()
    {
        Canvas.preWillRenderCanvases -= UpdateHintPosition;
    }

    private void Start()
    {
        if (Ep1TutorialManager.IsSkippedForTesting)
        {
            FinishHint();
            return;
        }

        if (!IsTalismanHint && _hintType != Ep1TutorialHintType.Parry && Ep1TutorialManager.HasCompletedHint(_hintType))
        {
            FinishHint();
            return;
        }

        _player = FindFirstObjectByType<Player>();
        _camera = Camera.main;
        if (_player == null || _camera == null)
        {
            enabled = false;
            return;
        }
        _input = _player.GetComponent<PlayerInputHandler>();
        var canvasObject = new GameObject("HighJumpHintUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        _canvas = canvasObject.GetComponent<RectTransform>();
        _hintCanvas = canvasObject.GetComponent<Canvas>();
        _hintCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _hintCanvas.sortingOrder = 32765;
        _hintCanvas.pixelPerfect = true;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        _hint = CreateRect("Hint", _canvas, Vector2.zero, new Vector2(450f, 130f));
        _messageText = AddText(_message, new Vector2(0f, _showJumpKeys ? 40f : 0f), _showJumpKeys ? 450f : 760f);
        if (_showJumpKeys)
        {
            // Center the full C / separator / SPACE row as a group.
            _cKeyRect = AddKey(_cKey, new Vector2(-102f, -22f), 64f);
            _separatorText = AddText("또는", new Vector2(-32f, -22f), 60f);
            _spaceKeyRect = AddKey(_spaceKey, new Vector2(79f, -22f), 110f);
        }
        ApplyPresentation();
        _hintGroup = _hint.gameObject.AddComponent<CanvasGroup>();
        _hintGroup.alpha = 0f;
        _hintGroup.interactable = false;
        _hintGroup.blocksRaycasts = false;
        _hint.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (Ep1TutorialManager.IsSkippedForTesting)
        {
            FinishHint();
            return;
        }

        UpdateHintFade();
        if (_finishing)
        {
            // 완료 / 제거 요청 후에는 다 사라질 때까지 페이드만 진행
            if (_hint == null || !_hint.gameObject.activeSelf)
            {
                CompleteFinish();
            }
            return;
        }

        if (_hint == null || _player == null || _input == null) return;
        bool respawning = StageManager.Instance != null && StageManager.Instance.IsRespawning;
        if (_shown && (_player.Health.IsDead || respawning))
        {
            if (IsRequiredPracticeHint)
            {
                SetHintVisible(false);
                _shown = false;
                _jumpStarted = false;
                _heldLongEnough = false;
                return;
            }
            FinishHint();
            return;
        }
        bool available = !PauseController.IsPaused && !SceneArrivalFade.IsTransitioning &&
            !respawning && !_player.Health.IsDead && _input.enabled;
        if (!available)
        {
            // 일시정지 / 튜토리얼 카드 위에 힌트가 남아 보이지 않도록 바로 숨김 (돌아오면 다시 페이드 인)
            HideHintImmediately();
            return;
        }
        if (_tutorialManager != null && !_tutorialManager.CanShowHint(_hintType))
        {
            SetHintVisible(false);
            return;
        }
        if (!IsTalismanHint && _hintType != Ep1TutorialHintType.Parry && Ep1TutorialManager.HasCompletedHint(_hintType))
        {
            FinishHint();
            return;
        }
        if (!_shown)
        {
            float distance = _tutorialManager != null ? _tutorialManager.TriggerDistance : _triggerDistance;
            bool forceParry = _tutorialManager != null && _tutorialManager.ShouldStartParryHint;
            bool forceHighJump = _tutorialManager != null && _tutorialManager.ShouldStartHighJumpHint;
            bool withinRange = _tutorialManager != null ?
                _tutorialManager.IsWithinHintRange(_hintType, _player.transform.position, transform.position) :
                Vector2.Distance(_player.transform.position, transform.position) <= distance;
            // 부적 / 마무리 / 물약 연습 힌트는 매니저가 띄울 때를 정하므로 거리 조건 없음
            if (!IsTalismanHint && _hintType != Ep1TutorialHintType.Finisher &&
                _hintType != Ep1TutorialHintType.PotionUse &&
                !(_hintType == Ep1TutorialHintType.Parry && forceParry) &&
                !(_hintType == Ep1TutorialHintType.HighJump && forceHighJump) &&
                !withinRange)
            {
                return;
            }
            // Display history must not stand in for successfully practicing the action.
            Ep1TutorialManager.TryMarkHintShown(_hintType);
            _shown = true;
            if (_tutorialManager != null) _tutorialManager.NotifyHintDisplayed(_hintType);
        }
        SetHintVisible(true);
        if (IsRequiredPracticeHint && !_showJumpKeys) return;
        if (!_showJumpKeys)
        {
            _displayElapsed += Time.deltaTime;
            float duration = _tutorialManager != null ? _tutorialManager.DisplayDuration : _displayDuration;
            if (_displayElapsed >= duration)
            {
                FinishHint();
            }
            return;
        }
        // 판정은 실제 점프 높이와 같은 기준 (PlayerMovement가 높은 점프로 확정했는지).
        // 점프 도중에 힌트가 떠도 이번 점프의 결과를 그대로 읽으므로 놓치지 않음
        bool jumping = _player.StateMachine.CurrentState == _player.JumpState;
        if (jumping && !_jumpStarted)
        {
            _jumpStarted = true;
            _heldLongEnough = false;
        }
        if (_jumpStarted && jumping && _player.Movement.IsLongJump)
        {
            _heldLongEnough = true;
        }
        if (_jumpStarted && !jumping && _player.IsGrounded)
        {
            if (_heldLongEnough)
            {
                if (_tutorialManager != null) _tutorialManager.CompleteHint(_hintType);
                FinishHint();
            }
            _jumpStarted = false;
        }
    }

    /// <summary>힌트를 페이드 아웃한 뒤 끔. 이미 안 보이면 바로 끔</summary>
    private void FinishHint()
    {
        if (_hint == null || !_hint.gameObject.activeSelf)
        {
            CompleteFinish();
            return;
        }
        _finishing = true;
        _hintVisible = false;
    }

    /// <summary>매니저가 연습 힌트를 치울 때: 페이드 아웃 후 오브젝트 제거</summary>
    internal void FadeOutAndDestroy()
    {
        _destroyWhenHidden = true;
        if (!enabled || _hint == null || !_hint.gameObject.activeSelf)
        {
            Destroy(gameObject);
            return;
        }
        FinishHint();
    }

    private void CompleteFinish()
    {
        if (_hint != null)
        {
            _hint.gameObject.SetActive(false);
        }
        enabled = false;
        if (_destroyWhenHidden)
        {
            Destroy(gameObject);
        }
    }

    private void SetHintVisible(bool visible)
    {
        _hintVisible = visible;
        if (visible && _hint != null && !_hint.gameObject.activeSelf)
        {
            _hint.gameObject.SetActive(true);
        }
    }

    private void HideHintImmediately()
    {
        _hintVisible = false;
        _hintAlpha = 0f;
        if (_hintGroup != null)
        {
            _hintGroup.alpha = 0f;
        }
        if (_hint != null)
        {
            _hint.gameObject.SetActive(false);
        }
    }

    private void UpdateHintFade()
    {
        if (_hint == null || _hintGroup == null) return;
        // 튜토리얼 일시정지 중에도 자연스럽게 끝나도록 실제 시간 기준
        _hintAlpha = Mathf.MoveTowards(_hintAlpha, _hintVisible ? 1f : 0f, Time.unscaledDeltaTime / _fadeDuration);
        _hintGroup.alpha = _hintAlpha;
        if (!_hintVisible && _hintAlpha <= 0f && _hint.gameObject.activeSelf)
        {
            _hint.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (_tutorialManager != null && _presentationVersion != _tutorialManager.PresentationVersion)
            ApplyPresentation();
    }

    private void UpdateHintPosition()
    {
        if (_hint == null || !_hint.gameObject.activeSelf || _player == null || _camera == null) return;
        Collider2D body = _player.GetComponent<Collider2D>();
        Vector3 top = body != null
            ? new Vector3(body.bounds.center.x, body.bounds.max.y, _player.transform.position.z)
            : _player.transform.position + Vector3.up * 2f;
        Rigidbody2D rigidbody = body != null ? body.attachedRigidbody : null;
        if (rigidbody != null && rigidbody.interpolation != RigidbodyInterpolation2D.None)
        {
            // Collider bounds use the physics pose; the sprite uses the interpolated transform.
            Vector2 renderOffset = (Vector2)rigidbody.transform.position - rigidbody.position;
            top += (Vector3)renderOffset;
        }
        // Follow the final camera pose, after camera movement, shake and zoom in LateUpdate.
        // Run before the canvas rebuild so text vertices use this frame's pixel alignment.
        Vector2 screen = _camera.WorldToScreenPoint(top);
        Vector2 offset = _tutorialManager != null ? _tutorialManager.HeadOffset : _headOffset;
        screen += offset * _hintCanvas.scaleFactor;
        screen.x = Mathf.Round(screen.x);
        screen.y = Mathf.Round(screen.y);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, screen, null, out Vector2 position))
            _hint.anchoredPosition = position;
    }

    private void ApplyPresentation()
    {
        if (_tutorialManager == null || _messageText == null) return;
        _messageText.text = _tutorialManager.GetMessage(_hintType);
        Vector2 textSize = _showJumpKeys ? _tutorialManager.JumpTextSize : _tutorialManager.InfoTextSize;
        _messageText.rectTransform.sizeDelta = textSize;
        Sprite hintKey = _hintType == Ep1TutorialHintType.Guard ? _tutorialManager.GuardKeySprite :
            IsTalismanHint ? _tutorialManager.TalismanKeySprite :
            _hintType == Ep1TutorialHintType.PotionUse ? _tutorialManager.PotionKeySprite : null;
        if (hintKey != null)
        {
            if (_cKeyRect == null)
                _cKeyRect = AddKey(hintKey, new Vector2(0f, -36f), 64f);
            _messageText.rectTransform.anchoredPosition = new Vector2(0f, 36f + (textSize.y - 52f) * 0.5f);
        }
        _tutorialManager.ApplyTextStyle(_messageText);
        textSize.y = Mathf.Max(textSize.y, _messageText.preferredHeight);
        _messageText.rectTransform.sizeDelta = textSize;
        if (_separatorText != null)
        {
            _separatorText.text = _tutorialManager.KeySeparator;
            _tutorialManager.ApplyTextStyle(_separatorText);
            float separatorWidth = Mathf.Max(60f, _separatorText.preferredWidth);
            _separatorText.rectTransform.sizeDelta = new Vector2(separatorWidth, 52f);
            float separatorHeight = Mathf.Max(52f, _separatorText.preferredHeight);
            _separatorText.rectTransform.sizeDelta = new Vector2(separatorWidth, separatorHeight);
            float extraWidth = (separatorWidth - 60f) * 0.5f;
            _cKeyRect.anchoredPosition = new Vector2(-102f - extraWidth, -22f);
            _spaceKeyRect.anchoredPosition = new Vector2(79f + extraWidth, -22f);
            // Grow upward so wrapped text stays clear of the key row below it.
            _messageText.rectTransform.anchoredPosition = new Vector2(0f,
                40f + (textSize.y - 52f) * 0.5f + (separatorHeight - 52f) * 0.5f);
        }
        _presentationVersion = _tutorialManager.PresentationVersion;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = rect.anchorMin;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private Text AddText(string value, Vector2 position, float width)
    {
        RectTransform rect = CreateRect("Text", _hint, position, new Vector2(width, _showJumpKeys ? 52f : 100f));
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = _font;
        text.fontSize = 26;
        text.text = value;
        text.alignment = TextAnchor.MiddleCenter;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = Color.black;
        text.raycastTarget = false;
        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(1f, -1f);
        return text;
    }

    private RectTransform AddKey(Sprite sprite, Vector2 position, float width)
    {
        RectTransform rect = CreateRect("Key", _hint, position, new Vector2(width, 64f));
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return rect;
    }
}
