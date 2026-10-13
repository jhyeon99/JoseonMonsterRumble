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

    [Header("체크포인트 표시")]
    [SerializeField] private Sprite _inactiveSprite;
    [SerializeField] private Sprite _activeSprite;

    [Header("체크포인트 활성화 연출")]
    [SerializeField] private Animator _activationAnimator;
    [SerializeField] private AnimationClip _activationAnimation;

    private static readonly int ActivationState = Animator.StringToHash("Base Layer.SavePoint");
    private Coroutine _activationRoutine;

    private bool _hasWarnedMissingController;
    private SpriteRenderer _spriteRenderer;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_activationAnimator == null) _activationAnimator = GetComponent<Animator>();
        if (_inactiveSprite == null)
        {
            _inactiveSprite = _spriteRenderer.sprite;
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
            stageManager.ActivateCheckpoint(this);
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
        StopActivationAnimation();
        Sprite sprite = isActivated ? _activeSprite : _inactiveSprite;
        if (_spriteRenderer != null && sprite != null)
        {
            _spriteRenderer.sprite = sprite;
        }
    }

    public void PlayActivation()
    {
        if (_activationAnimator == null || _activationAnimation == null || !Application.isPlaying)
        {
            SetActivated(true);
            return;
        }

        StopActivationAnimation();
        _activationAnimator.enabled = true;
        _activationAnimator.speed = 1f;
        _activationAnimator.Rebind();
        _activationAnimator.Play(ActivationState, 0, 0f);
        _activationAnimator.Update(0f);
        _activationRoutine = StartCoroutine(FinishActivationAnimation());
    }

    private IEnumerator FinishActivationAnimation()
    {
        yield return new WaitForSeconds(_activationAnimation.length);
        _activationRoutine = null;
        SetActivated(true);
    }

    private void StopActivationAnimation()
    {
        if (_activationRoutine != null)
        {
            StopCoroutine(_activationRoutine);
            _activationRoutine = null;
        }
        if (_activationAnimator != null) _activationAnimator.enabled = false;
    }

    private void OnDisable()
    {
        StopActivationAnimation();
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
