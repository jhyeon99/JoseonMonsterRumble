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

#if UNITY_EDITOR
    [Header("에디터 히트박스 기즈모")]
    [Tooltip("에디터 비재생 상태에서 1, 2, 3타 히트박스를 모두 표시할지 여부")]
    [SerializeField] private bool _showAllHitboxesInEditor = true;

    [Tooltip("특정 타수만 단독 확인 (0: 전체, 1: 1타, 2: 2타, 3: 3타)")]
    [SerializeField, Range(0, 3)] private int _previewComboIndex = 0;

    [Tooltip("반투명 채움 볼륨 큐브 표시 여부")]
    [SerializeField] private bool _drawSolidHitbox = true;
#endif

    [SerializeField, ReadOnly] private int _currentCombo = 0;
    [SerializeField, ReadOnly] private bool _isHitboxActive;

    public bool IsHitboxActive => _isHitboxActive;
    public event System.Action<int> OnAttackStepStarted;
    public event System.Action<bool> OnAttackStepEnded;
    public event System.Action<HitData> OnMeleeImpact;
    private bool _stepActive;

    // 한 번의 공격 단계에서 같은 적은 한 번만 맞도록
    private readonly HashSet<IDamageable> _hitTargets = new HashSet<IDamageable>();
    private AttackData _activeStep;
    private Player _player;

    private void Awake()
    {
        _player = GetComponent<Player>();
    }

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
        CompleteAttackStep();
        EndHitbox();
        _hitTargets.Clear();
        _currentCombo = TryGetAttackData(index, out _) ? index + 1 : 0;
        if (_currentCombo > 0)
        {
            _stepActive = true;
            OnAttackStepStarted?.Invoke(_currentCombo);
        }
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

    public void CompleteAttackStep()
    {
        if (!_stepActive) return;
        _stepActive = false;
        OnAttackStepEnded?.Invoke(true);
    }

    public void StopAttack()
    {
        if (_stepActive)
        {
            _stepActive = false;
            OnAttackStepEnded?.Invoke(false);
        }
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

            // 잡몹(Hurtbox)은 기존처럼 몸 중심 ~ 외곽 사이, 보스처럼 큰 대상은 공격 판정과 겹치는 부분의 중심
            // (큰 콜라이더에 비율 보간을 쓰면 이펙트 / 소울이 몸 한가운데에서 나옴)
            Vector3 hitPoint = hit.TryGetComponent(out EnemyHurtbox _)
                ? Vector3.Lerp(hit.bounds.center, edgePoint, _hitPointCenterRatio)
                : GetOverlapCenter(center, _activeStep.HitboxSize, hit.bounds, edgePoint);
            Vector3 attackDir = (hit.bounds.center - transform.position).normalized;

            float finalDamage = _activeStep.Damage;
            bool isBuffed = _player != null && _player.WeaponBuff != null && _player.WeaponBuff.IsBuffActive;
            if (isBuffed)
            {
                finalDamage *= _player.WeaponBuff.DamageMultiplier;
            }

            var hitData = new HitData(
                finalDamage,
                _activeStep.KnockbackForce,
                hitPoint,
                attackDir,
                comboStep: _currentCombo,
                isBuffed: isBuffed
            );
            if (damageable.TakeDamage(hitData) && hitData.Damage > 0f)
                OnMeleeImpact?.Invoke(hitData);
        }
    }

    /// <summary>공격 판정 상자와 대상 콜라이더 영역이 겹치는 부분의 중심. 안 겹치면 fallback</summary>
    private static Vector3 GetOverlapCenter(Vector3 boxCenter, Vector2 boxSize, Bounds target, Vector3 fallback)
    {
        Vector2 boxMin = (Vector2)boxCenter - (boxSize * 0.5f);
        Vector2 boxMax = (Vector2)boxCenter + (boxSize * 0.5f);

        float minX = Mathf.Max(boxMin.x, target.min.x);
        float maxX = Mathf.Min(boxMax.x, target.max.x);
        float minY = Mathf.Max(boxMin.y, target.min.y);
        float maxY = Mathf.Min(boxMax.y, target.max.y);

        if (minX > maxX || minY > maxY)
        {
            return fallback;
        }

        return new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, boxCenter.z);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        UnityEngine.Object activeObj = UnityEditor.Selection.activeObject;
        if (activeObj == null) return;

        bool isPlayerOrChild = false;
        if (UnityEditor.Selection.activeGameObject != null)
        {
            GameObject selectedGO = UnityEditor.Selection.activeGameObject;
            isPlayerOrChild = (selectedGO == gameObject || selectedGO.transform.IsChildOf(transform));
        }

        bool isAttackDataSelected = (activeObj == _attackDataTableSO);

        // Player 본체, 자식 오브젝트(Visual 등), 또는 연결된 AttackData 에셋이 선택되었을 때 렌더링
        if (!isPlayerOrChild && !isAttackDataSelected)
        {
            return;
        }

        DrawHitboxGizmos();
    }
#endif

    private void OnDrawGizmosSelected()
    {
#if !UNITY_EDITOR
        DrawHitboxGizmos();
#endif
    }

    private void DrawHitboxGizmos()
    {
        if (_attackDataTableSO == null || _attackDataTableSO.Data == null ||
            _attackDataTableSO.Data.Length == 0 || _attackOrigin == null)
        {
            return;
        }

        // 플레이 중 실제 공격 판정이 활성화된 프레임에는 빨간색으로 점등 표시
        if (Application.isPlaying && _isHitboxActive)
        {
            int activeIndex = Mathf.Clamp(_currentCombo - 1, 0, _attackDataTableSO.Data.Length - 1);
            AttackData activeStep = _attackDataTableSO.Data[activeIndex];

            Vector3 activeCenter = _attackOrigin.position + _attackOrigin.TransformDirection(activeStep.HitboxOffset);
            Gizmos.color = new Color(1f, 0f, 0f, 0.35f);
            Gizmos.DrawCube(activeCenter, activeStep.HitboxSize);
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(activeCenter, activeStep.HitboxSize);
            return;
        }

#if UNITY_EDITOR
        var wireColors = new Color[]
        {
            new Color(0.2f, 0.7f, 1f, 1f),   // 1타: 하늘색
            new Color(0.2f, 1f, 0.4f, 1f),   // 2타: 연두색
            new Color(1f, 0.5f, 0.1f, 1f)    // 3타: 주황색
        };

        var solidColors = new Color[]
        {
            new Color(0.2f, 0.7f, 1f, 0.15f), // 1타 반투명 면
            new Color(0.2f, 1f, 0.4f, 0.15f), // 2타 반투명 면
            new Color(1f, 0.5f, 0.1f, 0.15f)  // 3타 반투명 면
        };

        for (int i = 0; i < _attackDataTableSO.Data.Length; i++)
        {
            if (!_showAllHitboxesInEditor && _previewComboIndex != (i + 1)) continue;
            if (_previewComboIndex != 0 && _previewComboIndex != (i + 1)) continue;

            AttackData step = _attackDataTableSO.Data[i];
            Vector3 center = _attackOrigin.position + _attackOrigin.TransformDirection(step.HitboxOffset);
            int colorIndex = Mathf.Clamp(i, 0, wireColors.Length - 1);

            if (_drawSolidHitbox)
            {
                Gizmos.color = solidColors[colorIndex];
                Gizmos.DrawCube(center, step.HitboxSize);
            }

            Gizmos.color = wireColors[colorIndex];
            Gizmos.DrawWireCube(center, step.HitboxSize);
        }
#endif
    }
}
