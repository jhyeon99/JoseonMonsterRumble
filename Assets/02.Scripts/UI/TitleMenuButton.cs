using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class TitleMenuButton : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameObject _selectFrame;
    [SerializeField] private TextMeshProUGUI _buttonText;

    [Header("색상 설정")]
    [SerializeField] private Color _selectedTextColor = new Color(1f, 0.98f, 0.9f, 1f);
    [SerializeField] private Color _normalTextColor = new Color(0.55f, 0.55f, 0.58f, 0.8f);

    [Header("폰트 크기 설정")]
    [SerializeField] private float _selectedFontSize = 30f;
    [SerializeField] private float _normalFontSize = 26f;

    [SerializeField] private bool _isDefaultSelected = false;

    public GameObject SelectFrame
    {
        get => _selectFrame;
        set => _selectFrame = value;
    }

    public TextMeshProUGUI ButtonText
    {
        get => _buttonText;
        set => _buttonText = value;
    }

    public bool IsDefaultSelected
    {
        get => _isDefaultSelected;
        set => _isDefaultSelected = value;
    }

    private void Awake()
    {
        if (_buttonText == null)
        {
            _buttonText = GetComponentInChildren<TextMeshProUGUI>();
        }
    }

    private void Start()
    {
        SetHighlight(_isDefaultSelected);
    }

    public void OnSelect(BaseEventData eventData)
    {
        SetHighlight(true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        SetHighlight(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }
        SetHighlight(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject)
        {
            SetHighlight(false);
        }
    }

    public void SetHighlight(bool isSelected)
    {
        if (_selectFrame != null)
        {
            _selectFrame.SetActive(isSelected);
        }

        if (_buttonText != null)
        {
            _buttonText.color = isSelected ? _selectedTextColor : _normalTextColor;
            _buttonText.fontSize = isSelected ? _selectedFontSize : _normalFontSize;
            _buttonText.fontStyle = isSelected ? FontStyles.Bold : FontStyles.Normal;
        }
    }
}
