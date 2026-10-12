using System;
using System.Collections.Generic;
using UnityEngine;

public static class AbilityItemProgress
{
    public const string DokkaebiGamtuId = "dokkaebi_gamtu";
    private const string SavePrefix = "AbilityItem.Owned.";
    private const int OwnedValue = 1;
    private static readonly Dictionary<string, bool> OwnedItems = new Dictionary<string, bool>();

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
        if (!OwnedItems.TryGetValue(itemId, out bool owned))
        {
            owned = PlayerPrefs.GetInt(SavePrefix + itemId, 0) == OwnedValue;
            OwnedItems.Add(itemId, owned);
        }
        return owned;
    }

    public static bool TryAcquireItem(AbilityItemData item)
    {
        if (item == null || !item.IsValid || HasItem(item.ItemId))
        {
            return false;
        }
        OwnedItems[item.ItemId] = true;
        PlayerPrefs.SetInt(SavePrefix + item.ItemId, OwnedValue);
        PlayerPrefs.Save();
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
                PlayerPrefs.DeleteKey(SavePrefix + items[i].ItemId);
            }
        }
        OwnedItems.Clear();
        PlayerPrefs.Save();
        OnProgressReset?.Invoke();
    }
}
