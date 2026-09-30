using System.Collections;
using UnityEngine;
using System.Collections.Generic;

public class PlayerAttack : MonoBehaviour
{
    [SerializeField] private AttackDataTableSO _attackDataTableSO;
    [SerializeField] private Transform _attackOrigin;
    [SerializeField] private LayerMask _enemyLayer;


    [SerializeField, ReadOnly] private int _currentCombo = 0;

    public float CurrentAttackDuration
    {
        get
        {
            if (_currentCombo <= 0 || _attackDataTableSO == null)
            {
                return 0f;
            }

            int index = _currentCombo - 1;
            if (index >= _attackDataTableSO.Data.Length)
            {
                return 0f;
            }

            return _attackDataTableSO.Data[index].ActiveDuration;
        }
    }

    private Coroutine _comboResetCoroutine;

    private Animator _animator;
    private PlayerInputHandler _inputHandler;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _inputHandler = GetComponent<PlayerInputHandler>();
    }

    private Coroutine _attackRoutine;

    public void ExecuteAttack()
    {
        if (_attackDataTableSO == null || _attackDataTableSO.Data == null ||
            _attackDataTableSO.Data.Length == 0)
        {
            return;
        }

        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
        }

        _attackRoutine = StartCoroutine(FullComboAttackRoutine());
    }

    public void StopAttack()
    {
        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }
        _currentCombo = 0;
    }

    private IEnumerator FullComboAttackRoutine()
    {
        int count = _attackDataTableSO.Data.Length;
        for (int i = 0; i < count; i++)
        {
            _currentCombo = i + 1;
            AttackData step = _attackDataTableSO.Data[i];
            yield return StartCoroutine(HitboxRoutine(step));

            float delay = 0.45f;
            yield return new WaitForSeconds(delay);
        }
        _currentCombo = 0;
        _attackRoutine = null;
    }

    private IEnumerator HitboxRoutine(AttackData step)
    {
        float elapsedTime = 0f;

        var hitTargets = new HashSet<IDamageable>();

        while (elapsedTime < step.ActiveDuration)
        {
            Vector3 center = _attackOrigin.position + _attackOrigin.TransformDirection(step.HitboxOffset);
            Collider2D[] hits = Physics2D.OverlapBoxAll(center, step.HitboxSize, 0f, _enemyLayer);

            foreach (Collider2D hit in hits)
            {
                if (hit.TryGetComponent<IDamageable>(out IDamageable damageable))
                {
                    if (hitTargets.Contains(damageable)) continue;
                    hitTargets.Add(damageable);

                    Vector3 hitPoint = hit.bounds.ClosestPoint(center);
                    Vector3 attackDir = (hit.transform.position - transform.position).normalized;

                    var hitData = new HitData(step.Damage, step.KnockbackForce, hitPoint, attackDir);
                    damageable.TakeDamage(hitData);
                }
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator ResetComboCorutine()
    {
        yield return new WaitForSeconds(_attackDataTableSO.ComboResetTime);
        _currentCombo = 0;
    }

    private void OnDrawGizmosSelected()
    {
        if (_attackDataTableSO == null || _attackDataTableSO.Data == null ||
            _attackDataTableSO.Data.Length == 0 || _attackOrigin == null)
        {
            return;
        }

        Gizmos.color = Color.red;
        int previewIndex = Mathf.Clamp(_currentCombo - 1, 0, _attackDataTableSO.Data.Length - 1);
        AttackData step = _attackDataTableSO.Data[previewIndex];

        Vector3 center = _attackOrigin.position + _attackOrigin.TransformDirection(step.HitboxOffset);
        Gizmos.DrawWireCube(center, step.HitboxSize);
    }
}