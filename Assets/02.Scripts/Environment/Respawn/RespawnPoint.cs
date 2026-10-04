using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class RespawnPoint : MonoBehaviour
{
    [SerializeField] private string _checkpointId = "Checkpoint";
    public string CheckpointId => _checkpointId;

    [SerializeField] private bool _isDefault;
    public bool IsDefault => _isDefault;

    public Vector2 Position => transform.position;

    [Header("세이브 연출 (임시)")]
    [SerializeField] private Color _inactiveColor = Color.white;
    [SerializeField] private Color _activeColor = new Color(1f, 0.85f, 0.4f, 1f);

    [Tooltip("세이브될 때 잠깐 켜질 오브젝트 (예: 'SAVED' 텍스트 자식 오브젝트)")]
    [SerializeField] private GameObject _saveNotice;

    // 임시 연출 값 (정식 연출로 교체 예정)
    private const float SaveNoticeDuration = 1.5f;
    private const float PunchScale = 1.3f;
    private const float PunchDuration = 0.25f;

    private bool _hasWarnedMissingController;
    private SpriteRenderer _spriteRenderer;
    private Vector3 _baseScale;
    private Coroutine _punchRoutine;
    private Coroutine _noticeRoutine;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _baseScale = transform.localScale;

        if (_saveNotice != null)
        {
            _saveNotice.SetActive(false);
        }

        SetActivated(false);

        BoxCollider2D trigger = GetComponent<BoxCollider2D>();
        if (!trigger.isTrigger)
        {
            Debug.LogWarning(
                $"{name}: BoxCollider2D의 Is Trigger가 꺼져 있습니다.",
                this
            );
        }

        if (string.IsNullOrWhiteSpace(_checkpointId))
        {
            Debug.LogWarning($"{name}: Checkpoint ID가 비어 있습니다.", this);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.layer != Constants.Layers.Player)
        {
            return;
        }

        StageManager stageManager = StageManager.Instance;
        if (stageManager != null)
        {
            // 새로 세이브된 경우에만 연출 (이미 활성화된 체크포인트를 다시 지나갈 때는 무시)
            if (stageManager.ActivateCheckpoint(this))
            {
                PlaySaveFeedback();
            }

            return;
        }

        if (_hasWarnedMissingController)
        {
            return;
        }

        _hasWarnedMissingController = true;
        Debug.LogWarning(
            $"{name}: StageManager가 없습니다.",
            this
        );
    }

    /// <summary>활성화 상태 표시만 바꿈 (씬 로드 후 상태 복원용, 연출 없음)</summary>
    public void SetActivated(bool isActivated)
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = isActivated ? _activeColor : _inactiveColor;
        }
    }

    private void PlaySaveFeedback()
    {
        SetActivated(true);

        if (_punchRoutine != null)
        {
            StopCoroutine(_punchRoutine);
        }
        _punchRoutine = StartCoroutine(PunchRoutine());

        if (_saveNotice != null)
        {
            if (_noticeRoutine != null)
            {
                StopCoroutine(_noticeRoutine);
            }
            _noticeRoutine = StartCoroutine(NoticeRoutine());
        }
    }

    private IEnumerator PunchRoutine()
    {
        float elapsed = 0f;

        while (elapsed < PunchDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / PunchDuration);

            // 0 → 1 → 0 으로 커졌다가 돌아옴
            float scale = Mathf.Lerp(1f, PunchScale, Mathf.Sin(t * Mathf.PI));
            transform.localScale = _baseScale * scale;
            yield return null;
        }

        transform.localScale = _baseScale;
        _punchRoutine = null;
    }

    private IEnumerator NoticeRoutine()
    {
        _saveNotice.SetActive(true);
        yield return new WaitForSeconds(SaveNoticeDuration);
        _saveNotice.SetActive(false);
        _noticeRoutine = null;
    }

    private void OnDisable()
    {
        transform.localScale = _baseScale;
        if (_saveNotice != null)
        {
            _saveNotice.SetActive(false);
        }

        _punchRoutine = null;
        _noticeRoutine = null;
    }

    private void OnDrawGizmos()
    {
        BoxCollider2D trigger = GetComponent<BoxCollider2D>();
        if (trigger == null)
        {
            return;
        }

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.color = _isDefault
            ? new Color(0f, 0.8f, 1f, 0.25f)
            : new Color(0f, 1f, 0.35f, 0.25f);
        Gizmos.DrawCube(trigger.offset, trigger.size);

        Gizmos.color = _isDefault ? Color.cyan : Color.green;
        Gizmos.DrawWireCube(trigger.offset, trigger.size);

        Gizmos.matrix = previousMatrix;
    }
}
