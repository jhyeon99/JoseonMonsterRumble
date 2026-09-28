using UnityEngine;

public class SoulItem : Item
{
    [SerializeField] private float _soulAmount;

    public override void OnCollect(Player player)
    {
        player.Soul.AddSoul(_soulAmount);
    }
}