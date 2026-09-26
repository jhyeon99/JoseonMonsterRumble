using System;
using UnityEngine;

public class EnemyFlyingReposition : MonoBehaviour
{
    [SerializeField] private Enemy _enemy;
    private Vector2 _direction;
    private float _angle;

    private void Update()
    {
        Rotation();
        Flip();
    }

    private void Rotation()
    {
        _direction = _enemy.GetDirectionToPlayer();
        _angle = Vector2.SignedAngle(Vector2.right, _direction);
        transform.rotation = Quaternion.Euler(0f, 0f, _angle - 180f);
    }

    private void Flip()
    {
        transform.localScale = _enemy.GetDirectionToPlayer().x > 0 ? new Vector3(1f, -1f, 1f) : new Vector3(1f, 1f, 1f);
    }
}