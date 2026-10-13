using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class MovingPlatformSwitch : MonoBehaviour, IDamageable
{
    [SerializeField] private MovingPlatform _platform;
    [SerializeField] private MovingPlatformEndpoint _targetEndpoint;

    private void Awake()
    {
        if (_platform == null)
        {
            Debug.LogWarning($"{name}: Moving Platform 참조가 없습니다.", this);
        }

        Collider2D switchCollider = GetComponent<Collider2D>();
        if (!switchCollider.isTrigger)
        {
            Debug.LogWarning($"{name}: Switch Collider가 Trigger가 아닙니다.", this);
        }
    }

    public bool TakeDamage(HitData hitData)
    {
        bool moved = _platform != null && _platform.TryMoveToEndpoint(_targetEndpoint);
        if (moved) AudioManager.Instance?.PlaySceneEvent("Environment.Switch", this);
        return moved;
    }
}
