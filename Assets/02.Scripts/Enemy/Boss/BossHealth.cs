using System;
using UnityEngine;

/// <summary>
/// 보스 체력. 체력바는 하나지만 페이즈마다 최대 체력이 따로 있음.
/// 1페이즈 체력이 0이 되면 하울링 동안 2페이즈 체력으로 다시 차오름.
/// 데미지를 받을지는 보스 상태가 결정 (Boss.CanTakeDamage).
/// </summary>
[RequireComponent(typeof(Boss))]
public class BossHealth : MonoBehaviour, IDamageable
{
    [Header("체력")]
    [SerializeField, Min(1f)] private float _phase1MaxHp = 300f;
    [SerializeField, Min(1f)] private float _phase2MaxHp = 400f;

    [Header("UI")]
    [Tooltip("비어 있으면 씬에서 자동으로 찾음 (꺼져 있어도 찾음)")]
    [SerializeField]
    private UI_BossHealthBar _healthBar;

    [Header("디버그")]
    [SerializeField] private bool _logDamage = true;

    [Header("상태 (확인용)")]
    [SerializeField, ReadOnly] private float _currentHp;
    [SerializeField, ReadOnly] private float _maxHp;

    /// <summary>체력 변경 (현재, 최대)</summary>
    public event Action<float, float> OnHpChanged;

    /// <summary>데미지를 받음 (피격 연출용)</summary>
    public event Action<HitData> OnDamaged;

    private Boss _boss;
    private HitFlash _hitFlash;

    public float CurrentHp => _currentHp;
    public float MaxHp => _maxHp;
    public bool LastDamageWasDuringGroggy { get; private set; }

    private void Awake()
    {
        _boss = GetComponent<Boss>();
        _hitFlash = GetComponent<HitFlash>();
        _maxHp = _phase1MaxHp;
        _currentHp = _phase1MaxHp;

        if (_healthBar == null)
        {
            _healthBar = FindFirstObjectByType<UI_BossHealthBar>(FindObjectsInactive.Include);
        }
    }

    /// <summary>전투 시작 시 1페이즈 체력으로 초기화하고 체력바 표시</summary>
    public void BeginFight()
    {
        _maxHp = _phase1MaxHp;
        _currentHp = _phase1MaxHp;

        if (_healthBar != null)
        {
            _healthBar.gameObject.SetActive(true);
            _healthBar.Initialize(_boss.BossName, _maxHp);
            _healthBar.Show();
        }

        OnHpChanged?.Invoke(_currentHp, _maxHp);
    }

    /// <summary>2페이즈 체력으로 다시 채움 (체력바는 refillDuration 동안 차오르는 연출)</summary>
    public void BeginPhase2(float refillDuration)
    {
        _maxHp = _phase2MaxHp;
        _currentHp = _phase2MaxHp;

        if (_healthBar != null)
        {
            _healthBar.StartPhase2(refillDuration);
        }

        OnHpChanged?.Invoke(_currentHp, _maxHp);
    }

    public void HideBar()
    {
        if (_healthBar != null)
        {
            _healthBar.Hide();
        }
    }

    public bool TakeDamage(HitData hitData)
    {
        // 소환 패턴 중에는 직접 공격 무효 (올챙이 기믹 데미지만)
        if (!_boss.CanTakeDirectDamage || hitData.Damage <= 0f)
        {
            return false;
        }

        // 보스는 넉백 없음
        LastDamageWasDuringGroggy = _boss.CurrentState is BossGroggyState;
        ApplyDamage(hitData.Damage);

        // 일반 적과 같은 피격 공통 반응 (타격 소울, 히트스톱, 카메라 흔들림)
        GameEvents.TriggerEnemyHit(hitData);
        OnDamaged?.Invoke(hitData);
        return true;
    }

    /// <summary>기믹 데미지 (3타로 날린 아기가 부딪힘 등). 받았으면 true</summary>
    public bool TakeGimmickDamage(float damage)
    {
        if (!_boss.CanTakeDamage || damage <= 0f)
        {
            return false;
        }

        _boss.Log($"기믹 데미지 {damage}");
        LastDamageWasDuringGroggy = _boss.CurrentState is BossGroggyState;
        ApplyDamage(damage);

        // 피격 연출용 (올챙이 충돌은 강한 피격으로 취급)
        OnDamaged?.Invoke(new HitData(damage, 0f, _boss.CenterPosition, Vector2.zero, comboStep: 3));
        return true;
    }

    private void ApplyDamage(float damage)
    {
        _currentHp = Mathf.Max(0f, _currentHp - damage);

        if (_logDamage)
        {
            Debug.Log($"[Boss] 데미지 {damage} → 체력 {_currentHp}/{_maxHp} (페이즈 {_boss.Phase})", this);
        }

        if (_hitFlash != null)
        {
            _hitFlash.Flash();
        }

        if (_healthBar != null)
        {
            _healthBar.UpdateHealth(_currentHp, _maxHp, false);
        }

        OnHpChanged?.Invoke(_currentHp, _maxHp);

        if (_currentHp <= 0f)
        {
            _boss.HandleHealthDepleted();
        }
    }
}
