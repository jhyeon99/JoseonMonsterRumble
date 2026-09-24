using UnityEngine;

public class PauseController : MonoBehaviour
{
    [SerializeField] private GameObject _pauseOverlay;
    [SerializeField, ReadOnly] private bool _isPaused;

    public static bool IsPaused { get; private set; }

    private float _previousTimeScale = 1f;

    private void Awake()
    {
        if (_pauseOverlay == null)
        {
            Debug.LogWarning($"{name}: Pause Overlay 참조가 없습니다.", this);
            enabled = false;
            return;
        }

        _isPaused = false;
        IsPaused = false;
        _pauseOverlay.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
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

    private void SetPaused(bool isPaused)
    {
        if (_isPaused == isPaused)
        {
            return;
        }

        _isPaused = isPaused;
        IsPaused = isPaused;
        _pauseOverlay.SetActive(isPaused);

        if (isPaused)
        {
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            return;
        }

        Time.timeScale = _previousTimeScale;
    }
}
