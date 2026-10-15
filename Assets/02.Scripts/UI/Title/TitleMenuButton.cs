using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TitleMenuButton : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler,
    IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, ISubmitHandler
{
    [SerializeField] private GameObject _selectFrame;
    [SerializeField] private TextMeshProUGUI _buttonText;
    [SerializeField] private Image _buttonImage;
    [SerializeField] private Sprite _normalSprite;
    [SerializeField] private Sprite _selectedSprite;

    [Header("색상 설정")]
    [SerializeField] private Color _selectedTextColor = new Color(1f, 0.98f, 0.9f, 1f);
    [SerializeField] private Color _normalTextColor = new Color(0.55f, 0.55f, 0.58f, 0.8f);
    [SerializeField] private Color _pressedTextColor = new Color(0.4f, 0.38f, 0.35f, 1f);

    [Header("폰트 크기 설정")]
    [SerializeField] private float _selectedFontSize = 30f;
    [SerializeField] private float _normalFontSize = 26f;

    [Header("누름 연출 설정")]
    [SerializeField] private float _pressedScaleRatio = 0.9f;
    [SerializeField] private float _actionDelay = 0.1f;

    [Header("호버 연출 설정")]
    [SerializeField] private AudioClip _hoverSound;
    [SerializeField] private float _hoverVolume = 1f;
    [SerializeField] private GameObject _hoverVfx;
    [SerializeField] private GameObject _hoverVfx2;

    [Header("효과음 및 액션 설정")]
    [SerializeField] private AudioClip _clickSound;
    [SerializeField] private float _clickVolume = 1f;
    [SerializeField] private UnityEvent _onClickAction;

    [SerializeField] private bool _isDefaultSelected;
    [SerializeField] private bool _isSelected;
    [SerializeField] private bool _isPressed;
    [SerializeField] private bool _isInteractable = true;

    private static bool _isAnyButtonClicked;
    private Vector3 _originalScale;
    private Coroutine _clickActionCoroutine;

    public UnityEvent OnClickAction => _onClickAction;

    /// <summary>동적으로 생성한 메뉴에도 기존 선택, 누름, 효과음을 적용한다.</summary>
    public void ConfigureVisuals(TextMeshProUGUI buttonText, GameObject selectionFrame, float fontSize)
    {
        _buttonText = buttonText;
        _selectFrame = selectionFrame;
        _normalFontSize = fontSize;
        _selectedFontSize = fontSize * 1.06f;
        _normalTextColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        _selectedTextColor = Color.white;
        _pressedTextColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        _pressedScaleRatio = 0.96f;
        if (_onClickAction == null) _onClickAction = new UnityEvent();
        ApplyHighlight(false);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        _isAnyButtonClicked = false;
    }

    private void Awake()
    {
        _originalScale = transform.localScale;
        if (_buttonText == null)
        {
            _buttonText = GetComponentInChildren<TextMeshProUGUI>();
        }
    }

    private void Start()
    {
        ApplyHighlight(_isDefaultSelected);
    }

    private void OnDisable()
    {
        if (_clickActionCoroutine != null)
        {
            StopCoroutine(_clickActionCoroutine);
            _clickActionCoroutine = null;
            _isAnyButtonClicked = false;
        }

        _isInteractable = true;
        _isPressed = false;
        _isSelected = false;
        ApplyPress(false);
        ApplyHighlight(false);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!_isInteractable || _isAnyButtonClicked)
        {
            return;
        }

        if (!_isSelected && Input.anyKeyDown)
        {
            PlayHoverSound();
        }
        ApplyHighlight(true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        ApplyHighlight(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_isInteractable || _isAnyButtonClicked)
        {
            return;
        }

        if (!_isSelected)
        {
            PlayHoverSound();
        }
        ApplyHighlight(true);

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_isPressed)
        {
            ApplyPress(false);
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        ApplyHighlight(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!_isInteractable || _isAnyButtonClicked)
        {
            return;
        }

        PlayClickSound();
        ApplyPress(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_isInteractable)
        {
            return;
        }

        ApplyPress(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        HandleClick();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        PlayClickSound();
        HandleClick();
    }

    public void ApplyHighlight(bool isSelected)
    {
        _isSelected = isSelected;

        if (_selectFrame != null)
        {
            _selectFrame.SetActive(isSelected);
        }

        if (_hoverVfx != null)
        {
            _hoverVfx.SetActive(isSelected);
        }

        if (_hoverVfx2 != null)
        {
            _hoverVfx2.SetActive(isSelected);
        }

        if (_buttonImage != null)
        {
            _buttonImage.sprite = isSelected && _selectedSprite != null ? _selectedSprite : _normalSprite;
        }

        if (_buttonText != null && !_isPressed)
        {
            _buttonText.color = isSelected ? _selectedTextColor : _normalTextColor;
            _buttonText.fontSize = isSelected ? _selectedFontSize : _normalFontSize;
            _buttonText.fontStyle = isSelected ? FontStyles.Bold : FontStyles.Normal;
        }
    }

    public void ApplyPress(bool isPressed)
    {
        _isPressed = isPressed;
        transform.localScale = isPressed ? _originalScale * _pressedScaleRatio : _originalScale;

        if (_buttonText != null)
        {
            _buttonText.color = isPressed ? _pressedTextColor : (_isSelected ? _selectedTextColor : _normalTextColor);
        }
    }

    private void HandleClick()
    {
        // Button.onClick may have closed the panel before this handler runs.
        if (!isActiveAndEnabled || !_isInteractable || _isAnyButtonClicked)
        {
            return;
        }

        if (_clickActionCoroutine != null)
        {
            StopCoroutine(_clickActionCoroutine);
        }

        _clickActionCoroutine = StartCoroutine(ClickActionRoutine());
    }

    private IEnumerator ClickActionRoutine()
    {
        _isInteractable = false;
        _isAnyButtonClicked = true;
        ApplyPress(true);

        yield return new WaitForSecondsRealtime(_actionDelay);

        ApplyPress(false);
        _isInteractable = true;
        _isAnyButtonClicked = false;
        _clickActionCoroutine = null;
        // The callback can disable or destroy this button. Finish cleanup first.
        _onClickAction?.Invoke();
    }

    private void PlayClickSound()
    {
        AudioManager manager = AudioManager.Instance;
        if (manager == null)
            return;
        manager.PlayUiEvent("UI.Click", _clickSound != null ? _clickSound : manager.ButtonClickClip, _clickVolume);
    }
    private void PlayHoverSound()
    {
        AudioManager manager = AudioManager.Instance;
        if (manager == null)
            return;
        manager.PlayUiEvent("UI.Hover", _hoverSound != null ? _hoverSound : manager.ButtonHoverClip, _hoverVolume);
    }
}
