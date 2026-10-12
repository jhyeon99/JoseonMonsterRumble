using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class AbilityItemPickup : MonoBehaviour
{
    private const string MissingDataWarning = "능력 아이템: 데이터 또는 Trigger Collider 설정이 없습니다.";
    private const string MissingViewWarning = "능력 아이템: 활성 획득 설명 UI가 없습니다. 아이템을 유지합니다.";
    private enum PickupPhase { Available, Collecting, Collected }

    [SerializeField] private AbilityItemData _item;
    [SerializeField, ReadOnly] private PickupPhase _phase;
    private Collider2D _trigger;
    private bool _hasWarnedMissingView;

    private void Awake()
    {
        _trigger = GetComponent<Collider2D>();
        if (_item == null || !_item.IsValid || !_trigger.isTrigger)
        {
            Debug.LogWarning(MissingDataWarning, this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (_item != null && AbilityItemProgress.HasItem(_item.ItemId))
        {
            _phase = PickupPhase.Collected;
            gameObject.SetActive(false);
            return;
        }
        _phase = PickupPhase.Available;
        _hasWarnedMissingView = false;
    }

    private void OnTriggerEnter2D(Collider2D other) => HandleContact(other);
    private void OnTriggerStay2D(Collider2D other) => HandleContact(other);

    private void HandleContact(Collider2D other)
    {
        if (_phase != PickupPhase.Available || PauseController.IsGameplayInputBlocked)
        {
            return;
        }
        Player player = other.GetComponentInParent<Player>();
        if (player == null || player.Health == null || player.Health.IsDead)
        {
            return;
        }
        ItemAcquisitionView view = ItemAcquisitionView.Instance;
        if (view == null)
        {
            if (!_hasWarnedMissingView)
            {
                Debug.LogWarning(MissingViewWarning, this);
                _hasWarnedMissingView = true;
            }
            return;
        }
        _phase = PickupPhase.Collecting;
        if (!view.TryAcquireItem(_item))
        {
            _phase = PickupPhase.Available;
            return;
        }
        _phase = PickupPhase.Collected;
        _trigger.enabled = false;
        gameObject.SetActive(false);
    }
}
