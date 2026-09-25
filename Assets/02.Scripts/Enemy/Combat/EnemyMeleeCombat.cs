using System.Collections;
using UnityEngine;

public class EnemyMeleeCombat : EnemyCombat
{
    [SerializeField] private Collider2D _attackCollider;
    private Coroutine _attackCoroutine;

    private void Awake()
    {
        _attackCollider.enabled = false;
    }

    public override void Attack()
    {
        if (_attackCoroutine != null) return;

        _attackCoroutine = StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        _attackCollider.enabled = true;

        yield return new WaitForSeconds(Enemy.Data.AttackDurationTime);

        _attackCollider.enabled = false;
        _attackCoroutine = null;
    }
}