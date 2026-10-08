using UnityEngine;

public class PotionItem : Item
{
    public override void OnSpawn()
    {
    }

    public override void OnCollect(Player player)
    {
        player.Health.AddPotion();
    }
}