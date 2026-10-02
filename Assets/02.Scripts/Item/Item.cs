using UnityEngine;

public abstract class Item : MonoBehaviour
{
    public abstract void OnSpawn();
    public abstract void OnCollect(Player player);

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent<Player>(out Player player))
        {
            OnCollect(player);
            gameObject.SetActive(false);
        }
    }
}