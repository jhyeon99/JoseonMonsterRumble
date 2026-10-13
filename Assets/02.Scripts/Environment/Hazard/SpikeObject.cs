using UnityEngine;

public class SpikeObject : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float _damageInterval = 0.05f;
    [SerializeField, Min(0f)] private float _damage = 5f;

    private float _nextDamageTime;

    private void OnEnable()
    {
        _nextDamageTime = 0f;
    }

    private void OnTriggerEnter2D(Collider2D other) => ApplyDamage(other);

    private void OnTriggerStay2D(Collider2D other) => ApplyDamage(other);

    private void OnCollisionEnter2D(Collision2D collision) => ApplyDamage(collision.collider);

    private void OnCollisionStay2D(Collision2D collision) => ApplyDamage(collision.collider);

    private void ApplyDamage(Collider2D other)
    {
        if (!isActiveAndEnabled || Time.time < _nextDamageTime)
            return;

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null)
            return;

        var hitData = new HitData(
            damage: _damage,
            knockbackForce: 0f,
            hitPoint: other.ClosestPoint(transform.position),
            attackDirection: Vector2.zero,
            attackName: "가시 접촉 피해"
        );
        hitData.Attacker = gameObject;

        if (playerHealth.TakeHazardDamage(hitData))
        {
            _nextDamageTime = Time.time + Mathf.Max(_damageInterval, 0.01f);
        }
    }
}
