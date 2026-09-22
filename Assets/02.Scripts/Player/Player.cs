using UnityEngine;

[RequireComponent(typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerAttack))]
public class Player : MonoBehaviour, IDamageable
{
    private PlayerHealth _health;
    public PlayerHealth Health => _health;
    private PlayerMovement _movement;
    public PlayerMovement Movement => _movement;
    private PlayerAttack _attack;
    public PlayerAttack Attack => _attack;

    private void Awake()
    {
        _health = GetComponent<PlayerHealth>();
        _movement = GetComponent<PlayerMovement>();
        _attack = GetComponent<PlayerAttack>();
    }

    public bool TakeDamage(HitData hitData)
    {
        if (Movement != null && Movement.IsDodging)
        {
            return false;
        }

        return Health.TakeDamage(hitData);
    }

    public void SetInvincible(bool isInvincible)
    {
        _health.SetInvincible(isInvincible);
    }
}