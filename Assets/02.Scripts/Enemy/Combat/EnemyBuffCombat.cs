using System.Collections;
using UnityEngine;

public class EnemyBuffCombat : EnemyCombat
{
    [SerializeField] private Collider2D _attackCollider;

    protected override void Awake()
    {
        base.Awake();
        _attackCollider.enabled = false;
    }

    public override void Attack()
    {
        _attackCollider.enabled = true;
    }
}