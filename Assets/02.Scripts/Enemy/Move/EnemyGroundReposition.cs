using System;
using UnityEngine;

public class EnemyGroundReposition : MonoBehaviour
{
    [SerializeField] private Enemy _enemy;
    private Vector2 _direction;

    private void Update()
    {
        Flip();
    }

    private void Flip()
    {
        transform.localScale = _enemy.GetDirectionToPlayer().x > 0 ? new Vector3(-1f, 1f, 1f) : new Vector3(1f, 1f, 1f);
    }
}