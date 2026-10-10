using UnityEngine;

public class EnemySpawnActivator : MonoBehaviour
{
    [Header("대상")]
    [SerializeField] private Transform _target;
    [SerializeField] private Camera _camera;

    [Header("활성화 범위")]
    [SerializeField] private Vector2 _extraRange = new Vector2(5f, 3f);

    [Header("검사 주기")]
    [SerializeField, Min(0.05f)] private float _checkInterval = 0.2f;

    private float _timer;
    private Enemy[] _enemies;

    private void Start()
    {
        if (_camera == null)
            _camera = Camera.main;


        if (_target == null)
        {
            var player = GameObject.FindWithTag(Constants.Tags.Player);
            if (player != null) _target = player.transform;
        }

        // 비활성화된 오브젝트도 포함해서 씬의 적을 탐색
        _enemies = FindObjectsByType<Enemy>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        // 처음에는 살아 있는 적만 비활성화
        foreach (Enemy enemy in _enemies)
        {
            if (enemy != null && !enemy.IsDefeated)
                enemy.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        _timer += Time.deltaTime;

        if (_timer < _checkInterval)
            return;

        _timer = 0f;
        CheckEnemies();
    }

    private void CheckEnemies()
    {
        if (_camera == null || _target == null)
            return;

        Vector3 cameraPosition = _camera.transform.position;

        float halfHeight =
            _camera.orthographicSize + _extraRange.y;

        float halfWidth =
            _camera.orthographicSize * _camera.aspect + _extraRange.x;

        foreach (Enemy enemy in _enemies)
        {
            if (enemy == null)
                continue;

            // 사망했거나 마무리 공격으로 날아가는 적은 제외
            if (enemy.IsDefeated)
                continue;

            Vector3 enemyPosition = enemy.transform.position;

            bool isNearCamera =
                Mathf.Abs(enemyPosition.x - cameraPosition.x) <= halfWidth &&
                Mathf.Abs(enemyPosition.y - cameraPosition.y) <= halfHeight;

            // 카메라 범위에 따라 활성화 상태 변경
            if (enemy.gameObject.activeSelf != isNearCamera)
            {
                enemy.gameObject.SetActive(isNearCamera);
            }
        }
    }
}