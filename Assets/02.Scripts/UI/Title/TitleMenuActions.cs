using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleMenuActions : MonoBehaviour
{
    private const string DefaultGameSceneName = "Test_Ep0";

    [SerializeField] private string _gameSceneName = DefaultGameSceneName;

    [Header("시작 전환")]
    [Tooltip("시작 버튼을 누른 뒤 화면과 타이틀 음악이 어두워지는 시간 (초). 이 동안 클릭 소리가 끝나서 로딩과 겹치지 않음")]
    [SerializeField, Min(0f)] private float _startFadeOutDuration = 0.5f;

    [Tooltip("EP0이 로드된 뒤 검은 화면이 걷히는 시간 (초). EP0은 자체 검은 화면에서 시작하므로 짧게")]
    [SerializeField, Min(0.01f)] private float _startFadeInDuration = 0.2f;

    private bool _isStarting;

    public void StartGame()
    {
        if (_isStarting)
        {
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(_gameSceneName))
        {
            Debug.LogWarning($"시작 씬 '{_gameSceneName}'이(가) 빌드 설정에 없습니다.", this);
            return;
        }

        _isStarting = true;
        StartCoroutine(StartGameRoutine());
    }

    /// <summary>화면 / 음악 페이드 아웃 → 진행 기록 초기화 → 비동기 로딩 (소리 재생과 씬 로딩이 겹치지 않게)</summary>
    private IEnumerator StartGameRoutine()
    {
        AudioManager audioManager = AudioManager.ExistingInstance;
        if (audioManager != null)
        {
            audioManager.StopBgm(_startFadeOutDuration);
        }

        yield return SceneArrivalFade.FadeOut(_startFadeOutDuration);
        SceneArrivalFade.Prepare(_startFadeInDuration);
        GameProgressReset.ResetAll();
        Time.timeScale = 1f;

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(_gameSceneName, LoadSceneMode.Single);
        if (loadOperation == null)
        {
            SceneArrivalFade.Cancel();
            _isStarting = false;
        }
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
