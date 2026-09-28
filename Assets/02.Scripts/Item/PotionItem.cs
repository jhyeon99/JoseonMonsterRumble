using UnityEngine;

public class PotionItem : Item
{
    [SerializeField] private float _healAmount;

    public override void OnCollect(Player player)
    {
        player.Health.Heal(_healAmount);
    }
}