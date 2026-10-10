using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class StageSceneExit : MonoBehaviour
{
    [Tooltip("이 영역에 플레이어가 들어오면 불러올 씬 이름입니다.")]
    [SerializeField] private string _nextSceneName = "Test_BossStage";

    [Tooltip("연결하면 입구를 맵 오른쪽 끝에 맞춥니다. 비워두면 오브젝트 위치를 사용합니다.")]
    [SerializeField] private StageCameraBounds _cameraBounds;

    [Tooltip("맵 오른쪽 끝에서 입구 중심까지의 거리입니다.")]
    [SerializeField, Min(0f)] private float _entryInset = 1f;

    [Header("씬 전환 페이드")]
    [SerializeField, Min(0f)] private float _fadeOutDuration = 0.5f;
    [SerializeField, Min(0.01f)] private float _fadeInDuration = 0.75f;

    private bool _hasRequestedTransition;
    private bool _hasWarnedMissingManager;

    private void Reset()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
        AlignWithBoundary();

        if (string.IsNullOrWhiteSpace(_nextSceneName) || !Application.CanStreamedLevelBeLoaded(_nextSceneName))
        {
            Debug.LogWarning($"{name}: '{_nextSceneName}' 씬을 Build Settings에서 찾을 수 없습니다.", this);
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        AlignWithBoundary();
    }

    private void AlignWithBoundary()
    {
        if (_cameraBounds == null)
            return;

        Vector3 position = transform.position;
        position.x = _cameraBounds.RightX - _entryInset;
        transform.position = position;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryChangeScene(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryChangeScene(other);
    }

    private void TryChangeScene(Collider2D other)
    {
        if (!enabled || _hasRequestedTransition || PauseController.IsPaused)
            return;

        PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
        if (player == null || player.IsDead)
            return;

        StageManager stageManager = StageManager.Instance;
        if (stageManager == null)
        {
            if (!_hasWarnedMissingManager)
            {
                _hasWarnedMissingManager = true;
                Debug.LogWarning($"{name}: StageManager가 없습니다.", this);
            }

            return;
        }

        _hasRequestedTransition = stageManager.TryLoadStageScene(_nextSceneName, _fadeOutDuration, _fadeInDuration);
    }

    private void OnDrawGizmosSelected()
    {
        BoxCollider2D trigger = GetComponent<BoxCollider2D>();
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(trigger.offset, trigger.size);
        Gizmos.matrix = previousMatrix;
    }
}
