using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class UI_AbilityItemSlots : MonoBehaviour
{
    private const string MissingSlotWarning = "능력 아이템 슬롯: 데이터 또는 아이콘 Image 참조가 없습니다.";
    [Serializable]
    private struct Slot
    {
        public AbilityItemData Item;
        public Image Icon;
    }
    [SerializeField] private Slot[] _slots;

    private void OnEnable()
    {
        AbilityItemProgress.OnItemAcquired += HandleItemAcquired;
        AbilityItemProgress.OnProgressReset += UpdateSlots;
        UpdateSlots();
    }

    private void OnDisable()
    {
        AbilityItemProgress.OnItemAcquired -= HandleItemAcquired;
        AbilityItemProgress.OnProgressReset -= UpdateSlots;
    }

    private void HandleItemAcquired(AbilityItemData item) => UpdateSlots();

    private void UpdateSlots()
    {
        if (_slots == null)
        {
            return;
        }
        for (int i = 0; i < _slots.Length; i++)
        {
            Slot slot = _slots[i];
            if (slot.Item == null || slot.Icon == null)
            {
                Debug.LogWarning(MissingSlotWarning, this);
                continue;
            }
            slot.Icon.sprite = slot.Item.Icon;
            slot.Icon.preserveAspect = true;
            slot.Icon.raycastTarget = false;
            slot.Icon.enabled = AbilityItemProgress.HasItem(slot.Item.ItemId);
        }
    }

    [ContextMenu("개발용: 연결된 아이템 획득 기록 초기화")]
    private void ResetItemProgress()
    {
        if (_slots == null)
        {
            return;
        }
        var items = new AbilityItemData[_slots.Length];
        for (int i = 0; i < _slots.Length; i++)
        {
            items[i] = _slots[i].Item;
        }
        AbilityItemProgress.ResetProgress(items);
    }
}
