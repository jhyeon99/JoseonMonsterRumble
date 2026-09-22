using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float _maxHealth;
    [SerializeField, ReadOnly] private float _currentHealth;
    private bool _isDead;
    public bool IsDead => _isDead;

    [SerializeField] private float _invincibleTime;

    private SpriteRenderer _spriteRenderer;

    private void Awake()
    {
        _currentHealth = _maxHealth;
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public bool TakeDamage(HitData hitdata)
    {
        if (IsDead) return false;

        _currentHealth = Mathf.Max(_currentHealth - hitdata.Damage, 0);
        StartCoroutine(InvincibleRoutine());

        if (_currentHealth <= 0)
        {
            _isDead = true;
        }

        return true;
    }

    private IEnumerator InvincibleRoutine()
    {
        SetInvincible(true);
        yield return new WaitForSeconds(_invincibleTime);
        SetInvincible(false);
    }

    public void SetInvincible(bool isInvincible)
    {
        int playerLayer = gameObject.layer;
        int enemyLayer = Constants.Layers.Enemy;

        if (enemyLayer != -1)
        {
            Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, isInvincible);
        }

        if (_spriteRenderer != null)
        {
            Color color = _spriteRenderer.color;
            color.a = isInvincible ? 0.5f : 1.0f;
            _spriteRenderer.color = color;
        }
    }
}