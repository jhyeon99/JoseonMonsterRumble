using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseController : MonoBehaviour
{
    [SerializeField] private GameObject _pauseOverlay;
    [SerializeField] private GameObject _pauseMenuPanel;
    [SerializeField] private GameObject _settingsPanel;
    [SerializeField] private GameObject _titleConfirmationPanel;
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _titleButton;
    [SerializeField] private Button _confirmYesButton;
    [SerializeField] private Selectable _settingsFirstSelectable;
    [SerializeField] private string _titleSceneName = "Test_TitleScene";
    [SerializeField, ReadOnly] private bool _isPaused;

    public static bool IsPaused { get; private set; }

    private float _previousTimeScale = 1f;
    [SerializeField, ReadOnly] private bool _isSettingsAvailable;

    private void Awake()
    {
        if (!HasRequiredReferences())
        {
            enabled = false;
            return;
        }

        _isPaused = false;
        IsPaused = false;
        _isSettingsAvailable = HasSettingsReferences();
        _pauseMenuPanel.SetActive(true);
        if (_settingsPanel != null)
        {
            _settingsPanel.SetActive(false);
        }

        if (!_isSettingsAvailable && _settingsButton != null)
        {
            _settingsButton.interactable = false;
        }

        _titleConfirmationPanel.SetActive(false);
        _pauseOverlay.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_isPaused && _isSettingsAvailable && _settingsPanel.activeSelf)
            {
                HideSettings();
                return;
            }

            if (_isPaused && _titleConfirmationPanel.activeSelf)
            {
                HideTitleConfirmation();
                return;
            }

            SetPaused(!_isPaused);
        }
    }

    private void OnDisable()
    {
        if (_isPaused)
        {
            SetPaused(false);
        }
    }

    public void PauseGame()
    {
        SetPaused(true);
    }

    public void ResumeGame()
    {
        SetPaused(false);
    }

    public void ShowSettings()
    {
        if (!_isPaused || !_isSettingsAvailable)
        {
            return;
        }

        _pauseMenuPanel.SetActive(false);
        _titleConfirmationPanel.SetActive(false);
        _settingsPanel.SetActive(true);
        Select(_settingsFirstSelectable);
    }

    public void HideSettings()
    {
        if (!_isSettingsAvailable)
        {
            return;
        }

        _settingsPanel.SetActive(false);
        _pauseMenuPanel.SetActive(true);
        Select(_settingsButton);
    }

    public void ShowTitleConfirmation()
    {
        if (!_isPaused)
        {
            return;
        }

        _pauseMenuPanel.SetActive(false);
        if (_settingsPanel != null)
        {
            _settingsPanel.SetActive(false);
        }

        _titleConfirmationPanel.SetActive(true);
        Select(_confirmYesButton);
    }

    public void HideTitleConfirmation()
    {
        _titleConfirmationPanel.SetActive(false);
        _pauseMenuPanel.SetActive(true);
        Select(_titleButton);
    }

    public void ReturnToTitle()
    {
        if (string.IsNullOrWhiteSpace(_titleSceneName))
        {
            Debug.LogWarning($"{name}: Title Scene 이름이 없습니다.", this);
            return;
        }

        RestoreTimeScale();
        _isPaused = false;
        IsPaused = false;
        SceneManager.LoadScene(_titleSceneName);
    }

    private void SetPaused(bool isPaused)
    {
        if (_isPaused == isPaused)
        {
            return;
        }

        _isPaused = isPaused;
        IsPaused = isPaused;

        if (isPaused)
        {
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            _pauseMenuPanel.SetActive(true);
            if (_settingsPanel != null)
            {
                _settingsPanel.SetActive(false);
            }

            _titleConfirmationPanel.SetActive(false);
            _pauseOverlay.SetActive(true);
            Select(_resumeButton);
            return;
        }

        _pauseOverlay.SetActive(false);
        RestoreTimeScale();
    }

    private bool HasRequiredReferences()
    {
        bool hasRequiredReferences = true;

        if (_pauseOverlay == null)
        {
            Debug.LogWarning($"{name}: Pause Overlay 참조가 없습니다.", this);
            hasRequiredReferences = false;
        }

        if (_pauseMenuPanel == null)
        {
            Debug.LogWarning($"{name}: Pause Menu Panel 참조가 없습니다.", this);
            hasRequiredReferences = false;
        }

        if (_titleConfirmationPanel == null)
        {
            Debug.LogWarning($"{name}: Title Confirmation Panel 참조가 없습니다.", this);
            hasRequiredReferences = false;
        }

        if (_resumeButton == null || _titleButton == null || _confirmYesButton == null)
        {
            Debug.LogWarning($"{name}: Pause 메뉴 버튼 참조가 없습니다.", this);
            hasRequiredReferences = false;
        }

        return hasRequiredReferences;
    }

    private bool HasSettingsReferences()
    {
        bool hasSettingsReferences = true;

        if (_settingsPanel == null)
        {
            Debug.LogWarning($"{name}: Settings Panel 참조가 없습니다.", this);
            hasSettingsReferences = false;
        }

        if (_settingsButton == null)
        {
            Debug.LogWarning($"{name}: Settings Button 참조가 없습니다.", this);
            hasSettingsReferences = false;
        }

        if (_settingsFirstSelectable == null)
        {
            Debug.LogWarning($"{name}: Settings 첫 선택 대상 참조가 없습니다.", this);
            hasSettingsReferences = false;
        }

        return hasSettingsReferences;
    }

    private void RestoreTimeScale()
    {
        Time.timeScale = _previousTimeScale;
    }

    private void Select(Selectable selectable)
    {
        if (EventSystem.current == null)
        {
            Debug.LogWarning($"{name}: EventSystem이 없어 UI 선택 상태를 설정할 수 없습니다.", this);
            return;
        }

        if (selectable == null)
        {
            Debug.LogWarning($"{name}: 선택할 UI 참조가 없습니다.", this);
            return;
        }

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(selectable.gameObject);
    }
}
