using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-200)]
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

    private static bool _menuPaused;
    private static Object _tutorialPauseOwner;
    public static bool IsPaused => _menuPaused || _tutorialPauseOwner != null;
    private static int _pauseTransitionFrame = -1;
    public static bool IsGameplayInputBlocked => IsPaused || Time.frameCount == _pauseTransitionFrame ||
                                                 (StageManager.Instance != null && StageManager.Instance.IsRespawning);

    [SerializeField, ReadOnly] private bool _isSettingsAvailable;
    private CanvasGroup _pauseMenuCanvasGroup;

    private void Awake()
    {
        if (!HasRequiredReferences())
        {
            enabled = false;
            return;
        }

        _isPaused = false;
        _menuPaused = false;
        _pauseTransitionFrame = -1;
        if (_pauseMenuPanel != null)
        {
            _pauseMenuCanvasGroup = _pauseMenuPanel.GetComponent<CanvasGroup>();
            if (_pauseMenuCanvasGroup == null)
            {
                _pauseMenuCanvasGroup = _pauseMenuPanel.AddComponent<CanvasGroup>();
            }
        }

        InitializeSettingsReferences();
        _isSettingsAvailable = HasSettingsReferences();
        _pauseMenuPanel.SetActive(true);
        SetPauseMenuInteractable(true);
        if (_settingsPanel != null)
        {
            _settingsPanel.SetActive(false);
            PauseAudioSettingsView audioSettingsView = _settingsPanel.GetComponent<PauseAudioSettingsView>();
            if (audioSettingsView != null)
            {
                audioSettingsView.OnCloseRequested.AddListener(HideSettings);
            }
        }

        if (!_isSettingsAvailable && _settingsButton != null)
        {
            _settingsButton.interactable = false;
        }
        else if (_isSettingsAvailable && _settingsButton != null)
        {
            _settingsButton.interactable = true;
        }

        _titleConfirmationPanel.SetActive(false);
        _pauseOverlay.SetActive(false);
    }

    private void Update()
    {
        if (_tutorialPauseOwner != null) return;
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

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayPanelOpen();
        }

        SetPauseMenuInteractable(false);
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

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayPanelClose();
        }

        _settingsPanel.SetActive(false);
        _pauseMenuPanel.SetActive(true);
        SetPauseMenuInteractable(true);
        Select(_settingsButton);
    }

    public void ShowTitleConfirmation()
    {
        if (!_isPaused)
        {
            return;
        }

        if (_settingsPanel != null)
        {
            _settingsPanel.SetActive(false);
        }

        SetPauseMenuInteractable(false);
        _titleConfirmationPanel.SetActive(true);
        Select(_confirmYesButton);
    }

    public void HideTitleConfirmation()
    {
        _titleConfirmationPanel.SetActive(false);
        SetPauseMenuInteractable(true);
        Select(_titleButton);
    }

    private void SetPauseMenuInteractable(bool isInteractable)
    {
        if (_pauseMenuCanvasGroup != null)
        {
            _pauseMenuCanvasGroup.interactable = isInteractable;
            _pauseMenuCanvasGroup.blocksRaycasts = isInteractable;
        }
    }

    public void ReturnToTitle()
    {
        if (string.IsNullOrWhiteSpace(_titleSceneName))
        {
            Debug.LogWarning($"{name}: Title Scene 이름이 없습니다.", this);
            return;
        }

        _isPaused = false;
        _menuPaused = false;
        RestoreTimeScale();
        SceneManager.LoadScene(_titleSceneName);
    }

    private void SetPaused(bool isPaused)
    {
        if (_tutorialPauseOwner != null) return;
        if (_isPaused == isPaused)
        {
            return;
        }

        _isPaused = isPaused;
        _menuPaused = isPaused;
        _pauseTransitionFrame = Time.frameCount;
        GameCursorController.SetPauseVisible(isPaused);

        if (isPaused)
        {
            Time.timeScale = 0f;
            _pauseMenuPanel.SetActive(true);
            SetPauseMenuInteractable(true);
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
        _titleConfirmationPanel.SetActive(false);
        SetPauseMenuInteractable(true);
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

    private void InitializeSettingsReferences()
    {
        if (_settingsPanel == null)
        {
            PauseAudioSettingsView audioSettingsView = GetComponentInChildren<PauseAudioSettingsView>(true);
            if (audioSettingsView != null)
            {
                _settingsPanel = audioSettingsView.gameObject;
            }
        }

        if (_settingsFirstSelectable == null && _settingsPanel != null)
        {
            _settingsFirstSelectable = _settingsPanel.GetComponentInChildren<Selectable>(true);
        }
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

    // 일시정지 직전 값(히트스톱 중이면 0.02 등)을 복원하면 슬로우모션이 남으므로 항상 정상 속도로 복귀
    private void RestoreTimeScale()
    {
        Time.timeScale = IsPaused ? 0f : 1f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPauseState()
    {
        _menuPaused = false;
        _tutorialPauseOwner = null;
        _pauseTransitionFrame = -1;
    }

    // Tutorial owns the pause without opening the Escape menu. Existing input and hit-stop
    // checks use IsPaused too, so they cannot resume time or accept attacks behind the card.
    public static bool TryPauseForTutorial(Object owner)
    {
        if (owner == null || IsPaused) return false;
        HitStop.Stop();
        _tutorialPauseOwner = owner;
        _pauseTransitionFrame = Time.frameCount;
        Time.timeScale = 0f;
        return true;
    }

    public static void ResumeFromTutorial(Object owner)
    {
        if (_tutorialPauseOwner != owner || owner == null) return;
        _tutorialPauseOwner = null;
        _pauseTransitionFrame = Time.frameCount;
        Time.timeScale = _menuPaused ? 0f : 1f;
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
