using UnityEngine;

public class EnemyBuffMove : MonoBehaviour
{
    private BuffEnemy _buffEnemy;
    private Enemy _enemy;
    private Rigidbody2D _rigidbody;
    private Vector2 _direction;

    private void Awake()
    {
        _buffEnemy = GetComponent<BuffEnemy>();
        _enemy = GetComponent<Enemy>();
        _rigidbody = GetComponent<Rigidbody2D>();
    }

    public void Move(Transform target)
    {
        _direction = target.position - transform.position;
        _rigidbody.linearVelocity = _direction * _enemy.Data.MoveSpeed;
    }

    public void HoverMove()
    {
    }

    public void StopMove()
    {
        _rigidbody.linearVelocity = Vector3.zero;
    }
}
