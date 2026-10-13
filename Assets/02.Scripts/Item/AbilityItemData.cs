using UnityEngine;

public enum AbilityItemEffect
{
    None,
    DodgeCounter
}

[CreateAssetMenu(menuName = "Items/Ability Item", fileName = "SO_AbilityItem")]
public sealed class AbilityItemData : ScriptableObject
{
    [SerializeField] private string _itemId;
    [SerializeField] private string _displayName;
    [SerializeField, TextArea] private string _description;
    [SerializeField] private Sprite _icon;
    [SerializeField] private AbilityItemEffect _effect;

    public string ItemId => _itemId;
    public string DisplayName => _displayName;
    public string Description => _description;
    public Sprite Icon => _icon;
    public AbilityItemEffect Effect => _effect;
    public bool IsValid => !string.IsNullOrWhiteSpace(_itemId) && _icon != null &&
                           !string.IsNullOrWhiteSpace(_displayName) && !string.IsNullOrWhiteSpace(_description);
}
