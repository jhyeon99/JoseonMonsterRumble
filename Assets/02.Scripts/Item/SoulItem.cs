using UnityEngine;

public class SoulItem : Item
{
    [SerializeField] private float _soulAmout;

    public override void OnCollect(Player player)
    {
        player.Soul.AddSoul(_soulAmout);
    }
}