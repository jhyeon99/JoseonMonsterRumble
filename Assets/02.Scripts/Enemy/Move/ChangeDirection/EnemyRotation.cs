using System;
using UnityEngine;

public class EnemyRotation : MonoBehaviour
{
    private Enemy _enemy;
    private Vector2 _direction;
    private float _angle;

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();
    }

    private void Update()
    {
        Rotation();
    }

    private void Rotation()
    {
        _direction = _enemy.GetDirectionToPlayer();
        _angle = Vector2.SignedAngle(Vector2.right, _direction);
        transform.rotation = _direction.x >= 0f
            ? Quaternion.Euler(0f, 0f, _angle - 164f)
            : Quaternion.Euler(0f, 0f, _angle - 196f);
    }
}
