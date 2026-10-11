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
    [SerializeField, Min(0.1f)] private float _holdDuration = 0.3f;
    [SerializeField] private Vector2 _headOffset = new Vector2(0f, 95f);
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
    private bool _shown;
    private bool _jumpStarted;
    private bool _heldLongEnough;
    private float _heldTime;
    private float _displayElapsed;

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

    private void Start()
    {
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
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32765;
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
        _hint.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (_hint == null || _player == null || _input == null) return;
        bool available = !PauseController.IsPaused && !SceneArrivalFade.IsTransitioning &&
            !_player.Health.IsDead && _input.enabled;
        if (!available)
        {
            _hint.gameObject.SetActive(false);
            return;
        }
        if (!_shown)
        {
            float distance = _tutorialManager != null ? _tutorialManager.TriggerDistance : _triggerDistance;
            if (Vector2.Distance(_player.transform.position, transform.position) > distance) return;
            _shown = true;
        }
        _hint.gameObject.SetActive(true);
        if (!_showJumpKeys)
        {
            _displayElapsed += Time.deltaTime;
            float duration = _tutorialManager != null ? _tutorialManager.DisplayDuration : _displayDuration;
            if (_displayElapsed >= duration)
            {
                _hint.gameObject.SetActive(false);
                enabled = false;
            }
            return;
        }
        bool jumping = _player.StateMachine.CurrentState == _player.JumpState;
        if (jumping && !_jumpStarted)
        {
            _jumpStarted = true;
            _heldTime = 0f;
        }
        if (_jumpStarted && jumping && !_player.IsGrounded && StageJumpInput.IsHeld)
        {
            _heldTime += Time.deltaTime;
            float holdDuration = _tutorialManager != null ? _tutorialManager.HoldDuration : _holdDuration;
            if (_heldTime >= holdDuration) _heldLongEnough = true;
        }
        else if (_jumpStarted && !_heldLongEnough)
        {
            _heldTime = 0f;
        }
        if (_jumpStarted && !jumping && _player.IsGrounded)
        {
            if (_heldLongEnough)
            {
                _hint.gameObject.SetActive(false);
                enabled = false;
            }
            _jumpStarted = false;
        }
    }

    private void LateUpdate()
    {
        if (_tutorialManager != null && _presentationVersion != _tutorialManager.PresentationVersion)
            ApplyPresentation();
        if (_hint == null || !_hint.gameObject.activeSelf || _player == null) return;
        Collider2D body = _player.GetComponent<Collider2D>();
        Vector3 top = body != null
            ? new Vector3(body.bounds.center.x, body.bounds.max.y, _player.transform.position.z)
            : _player.transform.position + Vector3.up * 2f;
        Vector3 screen = _camera.WorldToScreenPoint(top);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, screen, null, out Vector2 position))
            _hint.anchoredPosition = position + (_tutorialManager != null ? _tutorialManager.HeadOffset : _headOffset);
    }

    private void ApplyPresentation()
    {
        if (_tutorialManager == null || _messageText == null) return;
        _messageText.text = _tutorialManager.GetMessage(_hintType);
        Vector2 textSize = _showJumpKeys ? _tutorialManager.JumpTextSize : _tutorialManager.InfoTextSize;
        _messageText.rectTransform.sizeDelta = textSize;
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
