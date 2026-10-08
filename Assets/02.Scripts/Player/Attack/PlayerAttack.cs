using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 근접 공격 판정 담당.
/// 언제 판정을 켜고 끌지는 PlayerAttackState가 AttackData의 HitStartRatio / HitEndRatio로 결정하고,
/// 여기서는 판정이 켜져 있는 동안 히트박스 검사만 한다.
/// </summary>
public class PlayerAttack : MonoBehaviour
{
    [SerializeField] private AttackDataTableSO _attackDataTableSO;
    [SerializeField] private Transform _attackOrigin;
    [SerializeField] private LayerMask _enemyLayer;
    [Tooltip("콜라이더 중심과 외곽 접점 사이 보간 비율 (0: 몸체 중심, 1: 외곽 표면)")]
    [SerializeField, Range(0f, 1f)] private float _hitPointCenterRatio = 0.35f;

    [SerializeField, ReadOnly] private int _currentCombo = 0;
    [SerializeField, ReadOnly] private bool _isHitboxActive;

    public bool IsHitboxActive => _isHitboxActive;

    // 한 번의 공격 단계에서 같은 적은 한 번만 맞도록
    private readonly HashSet<IDamageable> _hitTargets = new HashSet<IDamageable>();
    private AttackData _activeStep;

    public bool TryGetAttackData(int index, out AttackData attackData)
    {
        if (_attackDataTableSO != null && _attackDataTableSO.Data != null &&
            index >= 0 && index < _attackDataTableSO.Data.Length)
        {
            attackData = _attackDataTableSO.Data[index];
            return true;
        }

        attackData = default;
        return false;
    }

    /// <summary>
    /// 공격 단계 시작 (판정은 아직 켜지 않음). 콤보 단계가 바뀔 때마다 호출.
    /// </summary>
    public void StartAttackStep(int index)
    {
        EndHitbox();
        _hitTargets.Clear();
        _currentCombo = TryGetAttackData(index, out _) ? index + 1 : 0;
    }

    /// <summary>
    /// 판정 시작. PlayerAttackState가 HitStartRatio 지점에서 호출.
    /// </summary>
    public void BeginHitbox(int index)
    {
        if (!TryGetAttackData(index, out AttackData attackData))
        {
            return;
        }

        _activeStep = attackData;
        _isHitboxActive = true;

        // 판정 구간이 아주 짧아서 같은 프레임에 끝나더라도 최소 한 번은 검사
        CheckHits();
    }

    /// <summary>판정 끝. PlayerAttackState가 HitEndRatio 지점에서 호출.</summary>
    public void EndHitbox()
    {
        _isHitboxActive = false;
    }

    public void StopAttack()
    {
        EndHitbox();
        _hitTargets.Clear();
        _currentCombo = 0;
    }

    private void Update()
    {
        if (_isHitboxActive)
        {
            CheckHits();
        }
    }

    private void CheckHits()
    {
        if (_attackOrigin == null) return;

        Vector3 center = _attackOrigin.position + _attackOrigin.TransformDirection(_activeStep.HitboxOffset);
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, _activeStep.HitboxSize, 0f, _enemyLayer);

        foreach (Collider2D hit in hits)
        {
            if (!EnemyHurtbox.TryGetDamageTarget(hit, out IDamageable damageable)) continue;
            if (!_hitTargets.Add(damageable)) continue;

            Vector3 edgePoint = hit.ClosestPoint(center);
            var hitPoint = Vector3.Lerp(hit.bounds.center, edgePoint, _hitPointCenterRatio);
            Vector3 attackDir = (hit.bounds.center - transform.position).normalized;

            var hitData = new HitData(
                _activeStep.Damage,
                _activeStep.KnockbackForce,
                hitPoint,
                attackDir,
                comboStep: _currentCombo
            );
            damageable.TakeDamage(hitData);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_attackDataTableSO == null || _attackDataTableSO.Data == null ||
            _attackDataTableSO.Data.Length == 0 || _attackOrigin == null)
        {
            return;
        }

        int previewIndex = Mathf.Clamp(_currentCombo - 1, 0, _attackDataTableSO.Data.Length - 1);
        AttackData step = _attackDataTableSO.Data[previewIndex];

        // 판정이 켜져 있으면 빨강, 아니면 회색 (플레이 중 타이밍 확인용)
        Gizmos.color = _isHitboxActive ? Color.red : new Color(1f, 1f, 1f, 0.3f);
        Vector3 center = _attackOrigin.position + _attackOrigin.TransformDirection(step.HitboxOffset);
        Gizmos.DrawWireCube(center, step.HitboxSize);
    }
}
