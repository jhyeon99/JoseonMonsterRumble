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
        if (scene.name != "Test_Ep1") return;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform point in root.GetComponentsInChildren<Transform>(true))
            {
                string pointName = point.name.ToLowerInvariant();
                string prefabName;
                if (pointName == "highjumphintpoint") prefabName = "Ep1HighJumpHint";
                else if (pointName == "respawnhint") prefabName = "Ep1RespawnHint";
                else if (pointName == "parryhint" || pointName == "parryhitn") prefabName = "Ep1ParryHint";
                else continue;
                if (point.GetComponentInChildren<Ep1HighJumpHint>(true) != null) continue;
                GameObject prefab = Resources.Load<GameObject>(prefabName);
                if (prefab != null) Instantiate(prefab, point, false);
            }
        }
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
        AddText(_message, new Vector2(0f, _showJumpKeys ? 40f : 0f), _showJumpKeys ? 450f : 760f);
        if (_showJumpKeys)
        {
            // Center the full C / separator / SPACE row as a group.
            AddKey(_cKey, new Vector2(-102f, -22f), 64f);
            AddText("또는", new Vector2(-32f, -22f), 60f);
            AddKey(_spaceKey, new Vector2(79f, -22f), 110f);
        }
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
            if (Vector2.Distance(_player.transform.position, transform.position) > _triggerDistance) return;
            _shown = true;
        }
        _hint.gameObject.SetActive(true);
        if (!_showJumpKeys)
        {
            _displayElapsed += Time.deltaTime;
            if (_displayElapsed >= _displayDuration)
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
            if (_heldTime >= _holdDuration) _heldLongEnough = true;
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
        if (_hint == null || !_hint.gameObject.activeSelf || _player == null) return;
        Collider2D body = _player.GetComponent<Collider2D>();
        Vector3 top = body != null
            ? new Vector3(body.bounds.center.x, body.bounds.max.y, _player.transform.position.z)
            : _player.transform.position + Vector3.up * 2f;
        Vector3 screen = _camera.WorldToScreenPoint(top);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, screen, null, out Vector2 position))
            _hint.anchoredPosition = position + _headOffset;
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

    private void AddText(string value, Vector2 position, float width)
    {
        RectTransform rect = CreateRect("Text", _hint, position, new Vector2(width, _showJumpKeys ? 52f : 100f));
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = _font;
        text.fontSize = 26;
        text.text = value;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.black;
        text.raycastTarget = false;
        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(1f, -1f);
    }

    private void AddKey(Sprite sprite, Vector2 position, float width)
    {
        RectTransform rect = CreateRect("Key", _hint, position, new Vector2(width, 64f));
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }
}
