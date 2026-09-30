using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleMenuActions : MonoBehaviour
{
    [SerializeField] private string _gameSceneName = "Test_CanvasStructure";

    public void StartGame()
    {
        if (!Application.CanStreamedLevelBeLoaded(_gameSceneName))
        {
            Debug.LogWarning($"시작 씬 '{_gameSceneName}'이(가) 빌드 설정에 없습니다.", this);
            return;
        }

        SceneManager.LoadScene(_gameSceneName);
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
