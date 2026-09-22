using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using TMPro;

public class TitleMenuButton : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, ISubmitHandler
{
    [SerializeField] private GameObject _selectFrame;
    [SerializeField] private TextMeshProUGUI _buttonText;

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

    [Header("효과음 및 액션 설정")]
    [SerializeField] private AudioClip _clickSound;
    [SerializeField] private float _clickVolume = 1f;
    [SerializeField] private UnityEvent _onClickAction;

    [SerializeField] private bool _isDefaultSelected = false;
    [SerializeField] private bool _isSelected = false;
    [SerializeField] private bool _isPressed = false;
    [SerializeField] private bool _isInteractable = true;

    private static bool _isAnyButtonClicked = false;

    private Coroutine _clickActionCoroutine;

    public GameObject SelectFrame => _selectFrame;
    public TextMeshProUGUI ButtonText => _buttonText;
    public bool IsDefaultSelected => _isDefaultSelected;

    private void Awake()
    {
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
        ApplyPress(false);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (!_isInteractable || _isAnyButtonClicked) return;
        ApplyHighlight(true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        ApplyHighlight(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_isInteractable || _isAnyButtonClicked) return;

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }
        else
        {
            ApplyHighlight(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_isPressed)
        {
            ApplyPress(false);
        }

        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject)
        {
            ApplyHighlight(false);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!_isInteractable || _isAnyButtonClicked) return;
        ApplyPress(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_isInteractable) return;
        ApplyPress(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        HandleClick();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        HandleClick();
    }

    public void ApplyHighlight(bool isSelected)
    {
        if (!_isSelected && isSelected)
        {
            PlayHoverSound();
        }

        _isSelected = isSelected;

        if (_selectFrame != null)
        {
            _selectFrame.SetActive(isSelected);
        }

        if (_hoverVfx != null)
        {
            _hoverVfx.SetActive(isSelected);
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

        transform.localScale = isPressed ? (Vector3.one * _pressedScaleRatio) : Vector3.one;

        if (_buttonText != null)
        {
            _buttonText.color = isPressed ? _pressedTextColor : (_isSelected ? _selectedTextColor : _normalTextColor);
        }
    }

    private void HandleClick()
    {
        if (!_isInteractable)
        {
            return;
        }

        StartClickAction();
    }

    private void StartClickAction()
    {
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
        PlayClickSound();
        ApplyPress(true);

        yield return new WaitForSecondsRealtime(_actionDelay);

        ApplyPress(false);
        _onClickAction?.Invoke();
        _isInteractable = true;
        _isAnyButtonClicked = false;
        _clickActionCoroutine = null;
    }

    private void PlayClickSound()
    {
        if (_clickSound == null)
        {
            return;
        }

        Vector3 soundPosition = Camera.main != null ? Camera.main.transform.position : transform.position;
        AudioSource.PlayClipAtPoint(_clickSound, soundPosition, _clickVolume);
    }

    private void PlayHoverSound()
    {
        if (_hoverSound == null)
        {
            return;
        }

        Vector3 soundPosition = Camera.main != null ? Camera.main.transform.position : transform.position;
        AudioSource.PlayClipAtPoint(_hoverSound, soundPosition, _hoverVolume);
    }
}
