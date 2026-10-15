using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 능력 아이템(도깨비 감투 등) 보유 여부. 게임을 실행하는 동안만 기억하고 디스크에 저장하지 않음
/// (게임을 껐다 켜면 초기화. 씬 이동 / 사망 리스폰에는 유지)
/// </summary>
public static class AbilityItemProgress
{
    public const string DokkaebiGamtuId = "dokkaebi_gamtu";
    private static readonly HashSet<string> OwnedItems = new HashSet<string>();

    public static event Action<AbilityItemData> OnItemAcquired;
    public static event Action OnProgressReset;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Initialize()
    {
        OwnedItems.Clear();
        OnItemAcquired = null;
        OnProgressReset = null;
    }

    public static bool HasItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        return OwnedItems.Contains(itemId);
    }

    public static bool TryAcquireItem(AbilityItemData item)
    {
        if (item == null || !item.IsValid || HasItem(item.ItemId))
        {
            return false;
        }

        OwnedItems.Add(item.ItemId);
        OnItemAcquired?.Invoke(item);
        return true;
    }

    public static void ResetProgress(AbilityItemData[] items)
    {
        if (items == null)
        {
            return;
        }

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null && !string.IsNullOrWhiteSpace(items[i].ItemId))
            {
                OwnedItems.Remove(items[i].ItemId);
            }
        }

        OnProgressReset?.Invoke();
    }
}
