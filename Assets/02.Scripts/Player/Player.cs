using UnityEngine;

[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerMovement))]
public class Player : MonoBehaviour, IDamageable
{
    private PlayerHealth _health;
    public PlayerHealth Health => _health;
    private PlayerMovement _movement;
    public PlayerMovement Movement => _movement;

    private void Awake()
    {
        _health = GetComponent<PlayerHealth>();
        _movement = GetComponent<PlayerMovement>();
    }

    public bool TakeDamage(float damage)
    {
        if (Movement != null && Movement.IsDodging)
        {
            return false;
        }

        return Health.TakeDamage(damage);
    }
}