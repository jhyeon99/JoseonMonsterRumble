using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class KillZone : MonoBehaviour
{
    [SerializeField] private string _deathReason = "추락";

    [SerializeField]
    private LayerMask _targetLayers =
        Constants.Layers.PlayerMask;

    private bool _hasWarnedMissingKillable;

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

        if (_targetLayers.value == 0)
        {
            Debug.LogWarning($"{name}: Target Layers가 비어 있습니다.", this);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!ContainsLayer(other.gameObject.layer))
        {
            return;
        }

        IKillable killable = other.GetComponentInParent<IKillable>();
        if (killable != null)
        {
            if (killable is PlayerHealth playerHealth)
            {
                playerHealth.Kill(_deathReason);
                return;
            }

            killable.Kill();
            return;
        }

        if (_hasWarnedMissingKillable)
        {
            return;
        }

        _hasWarnedMissingKillable = true;
        Debug.LogWarning(
            $"{name}: {other.name}에 IKillable 구현이 없습니다.",
            other
        );
    }

    private bool ContainsLayer(int layer)
    {
        return (_targetLayers.value & 1 << layer) != 0;
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

        Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
        Gizmos.DrawCube(trigger.offset, trigger.size);

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(trigger.offset, trigger.size);

        Gizmos.matrix = previousMatrix;
    }
}
