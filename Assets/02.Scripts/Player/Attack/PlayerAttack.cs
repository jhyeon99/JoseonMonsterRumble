using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

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

    public void ExecuteAttack()
    {
        if (_attackDataTableSO == null || _attackDataTableSO.Data == null ||
            _attackDataTableSO.Data.Length == 0)
        {
            return;
        }

        if (_comboResetCoroutine != null)
        {
            StopCoroutine(_comboResetCoroutine);
        }

        _currentCombo %= _attackDataTableSO.Data.Length;
        _currentCombo++;

        AttackData currentAttack = _attackDataTableSO.Data[_currentCombo - 1];

        StartCoroutine(HitboxRoutine(currentAttack));
        _comboResetCoroutine = StartCoroutine(ResetComboCorutine());
    }

    private IEnumerator HitboxRoutine(AttackData step)
    {
        float elapsedTime = 0f;

        while (elapsedTime < step.ActiveDuration)
        {
            Vector3 center = _attackOrigin.position + _attackOrigin.TransformDirection(step.HitboxOffset);
            Collider2D[] hits = Physics2D.OverlapBoxAll(center, step.HitboxSize, 0f, _enemyLayer);

            foreach (Collider2D hit in hits)
            {
                if (hit.TryGetComponent<IDamageable>(out IDamageable damageable))
                {
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