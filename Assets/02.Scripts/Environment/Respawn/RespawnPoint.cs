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

    private bool _hasWarnedMissingController;

    private void Awake()
    {
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

        DeathRespawnController controller = DeathRespawnController.Instance;
        if (controller != null)
        {
            controller.ActivateCheckpoint(this);
            return;
        }

        if (_hasWarnedMissingController)
        {
            return;
        }

        _hasWarnedMissingController = true;
        Debug.LogWarning(
            $"{name}: DeathRespawnController가 없습니다.",
            this
        );
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
