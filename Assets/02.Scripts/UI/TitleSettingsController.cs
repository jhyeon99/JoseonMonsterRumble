using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TitleSettingsController : MonoBehaviour
{
    [SerializeField] private GameObject _titleMenuContainer;
    [SerializeField] private GameObject _settingsPanel;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Selectable _settingsFirstSelectable;

    [SerializeField, ReadOnly] private bool _isReady;

    private void Awake()
    {
        _isReady = HasRequiredReferences();
        if (!_isReady)
        {
            enabled = false;
            return;
        }

        _titleMenuContainer.SetActive(true);
        _settingsPanel.SetActive(false);
    }

    private void Update()
    {
        if (_settingsPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            HideSettings();
        }
    }

    public void ShowSettings()
    {
        if (!_isReady)
        {
            return;
        }

        _titleMenuContainer.SetActive(false);
        _settingsPanel.SetActive(true);
        Select(_settingsFirstSelectable);
    }

    public void HideSettings()
    {
        if (!_isReady)
        {
            return;
        }

        _settingsPanel.SetActive(false);
        _titleMenuContainer.SetActive(true);
        Select(_settingsButton);
    }

    private bool HasRequiredReferences()
    {
        bool hasRequiredReferences = true;

        if (_titleMenuContainer == null)
        {
            Debug.LogWarning($"{name}: Title Menu Container 참조가 없습니다.", this);
            hasRequiredReferences = false;
        }

        if (_settingsPanel == null)
        {
            Debug.LogWarning($"{name}: Settings Panel 참조가 없습니다.", this);
            hasRequiredReferences = false;
        }

        if (_settingsButton == null)
        {
            Debug.LogWarning($"{name}: Settings Button 참조가 없습니다.", this);
            hasRequiredReferences = false;
        }

        if (_settingsFirstSelectable == null)
        {
            Debug.LogWarning($"{name}: Settings 첫 선택 대상 참조가 없습니다.", this);
            hasRequiredReferences = false;
        }

        return hasRequiredReferences;
    }

    private void Select(Selectable selectable)
    {
        if (EventSystem.current == null)
        {
            Debug.LogWarning($"{name}: EventSystem이 없어 UI 선택 상태를 설정할 수 없습니다.", this);
            return;
        }

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(selectable.gameObject);
    }
}
