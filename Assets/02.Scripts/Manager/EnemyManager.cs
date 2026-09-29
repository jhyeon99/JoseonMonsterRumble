using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    private static EnemyManager _instance = null;
    public static EnemyManager Instance => _instance;

    private void Awake()
    {
        if (_instance != null)
        {
            Destroy(gameObject);
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
