using System;
using UnityEngine;

public abstract class EnemyFlip : MonoBehaviour
{
    protected Enemy Enemy { get; private set; }
    private Vector2 _direction;

    private void Awake()
    {
        Enemy = GetComponent<Enemy>();
    }

    private void Update()
    {
        Flip();
    }

    protected abstract void Flip();

    private void OnTriggerEnter2D(Collider2D collider)
    {
        Debug.Log($"발사체가 {collider.gameObject.name}에 충돌함.");
    }
}
