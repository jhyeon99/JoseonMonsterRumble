using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerRangedAttack : MonoBehaviour
{
    private enum ChargeState
    {
        Idle,
        Charging,
        ReadyToFire
    }

    /// <summary>튜토리얼 연습 동안만 쓰는 충전 제한</summary>
    public enum TutorialChargeMode
    {
        None,
        SingleOnly,   // 충전 안 됨, 항상 한 장만 발사
        MultipleOnly  // 두 장 이상 충전해야 발사, 한 장에서 떼면 발사 없이 취소
    }

    private const int RequiredSlotCount = 5;

    [SerializeField] private Transform _flipTarget;

    [Header("부적 배치")]
    [SerializeField] private Transform _slotRoot;

    [Header("발사 범위 미리보기")]
    [SerializeField] private PlayerProjectile _rangePreviewProjectile;
    [SerializeField] private bool _showFireRange = true;

    [Header("충전")]
    [SerializeField, Min(0.01f)] private float _chargeInterval = 0.22f; // 누르고 있을 때 부적이 하나씩 추가되는 간격

    [Tooltip("차징이 가속된 뒤 도달하는 최소 슬롯 활성화 간격")]
    [SerializeField, Min(0.01f)] private float _minimumChargeInterval = 0.1f;

    [SerializeField, Min(0.01f)] private float _soulCostPerTalisman = 1f;

    [Header("데미지")]
    [SerializeField] private float _damageIncreasePercent = 0f;

    private readonly List<SpriteRenderer> _slots = new List<SpriteRenderer>();
    private readonly List<Animator> _slotAnimators = new List<Animator>();
    private readonly List<TalismanSlotVisualFeedback> _slotFeedbacks = new List<TalismanSlotVisualFeedback>();
    private static readonly int AnimParamActivate = Animator.StringToHash("activate");
    private static readonly int AnimParamCancel = Animator.StringToHash("cancel");
    private static readonly int AnimStateSpawn = Animator.StringToHash("Talisman_Spawn");

    private PlayerSoul _soul;

    private ChargeState _chargeState;
    private int _activeCount;
    private float _chargeTimer;
    private UnityEngine.Object _tutorialChargeOwner;
    private TutorialChargeMode _tutorialChargeMode;

    public bool IsCharging => _chargeState == ChargeState.Charging;
    public bool IsReadyToFire => _chargeState == ChargeState.ReadyToFire;
    public int ActiveCount => _activeCount;
    public int SlotCount => _slots.Count;
    public float SoulCostPerTalisman => _soulCostPerTalisman;

    /// <summary>발사 시 호출. 인자 = 실제로 발사된 부적 개수 (사운드, 이펙트 연결용)</summary>
    public event Action<int> OnFired;
    public event Action<int> OnChargeStep;
    public event Action OnChargeEnded;

    // 지금 활성화할 수 있는 최대 개수 (슬롯 수와 보유 soul 중 작은 값)
    private int MaxActivatable =>
        Mathf.Min(_slots.Count, Mathf.FloorToInt(_soul.CurrentSoul / _soulCostPerTalisman));

    // 충전으로 늘릴 수 있는 최대 개수 (튜토리얼 단발 연습 중에는 1)
    private int MaxChargeCount =>
        _tutorialChargeMode == TutorialChargeMode.SingleOnly ? Mathf.Min(1, MaxActivatable) : MaxActivatable;

    private void Awake()
    {
        _soul = GetComponent<PlayerSoul>();
        if (_flipTarget == null)
        {
            _flipTarget = transform;
        }

        InitializeSlots();
        InitializeFollowController();
    }

    private void Update()
    {
        if (_chargeState != ChargeState.Charging)
        {
            return;
        }

        _chargeTimer += Time.deltaTime;
        while (true)
        {
            float currentInterval = CalculateChargeInterval();
            if (_chargeTimer < currentInterval)
            {
                break;
            }

            _chargeTimer -= currentInterval;

            if (_activeCount >= MaxChargeCount)
            {
                continue;
            }

            _activeCount++;
            ActivateSlot(_activeCount - 1);
        }
    }

    private void LateUpdate()
    {
        RefreshSlotVisuals();
    }

    private void OnEnable()
    {
        if (_slotRoot != null)
        {
            _slotRoot.gameObject.SetActive(true);
        }
    }

    private void OnDisable()
    {
        CancelCharge();
        if (_slotRoot != null)
        {
            _slotRoot.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (_slotRoot != null)
        {
            Destroy(_slotRoot.gameObject);
        }
    }

    // ───────── 입력 ─────────

    /// <summary>원거리 공격 키를 누른 순간 호출</summary>
    public void PressAttack()
    {
        if (_chargeState != ChargeState.Idle)
        {
            return;
        }

        if (MaxActivatable < 1)
        {
            GetComponent<PlayerAudio>()?.PlayUnavailable();
            return;
        }

        _chargeState = ChargeState.Charging;
        _activeCount = 1;
        _chargeTimer = 0f;
        ActivateSlot(0);
    }

    public bool StopCharging()
    {
        if (_chargeState != ChargeState.Charging)
        {
            return false;
        }

        // 튜토리얼 여러 장 연습: 한 장만 충전된 채로 떼면 발사하지 않고 취소
        if (_tutorialChargeMode == TutorialChargeMode.MultipleOnly && _activeCount < 2)
        {
            ResetCharge();
            return false;
        }

        _chargeState = ChargeState.ReadyToFire;
        _chargeTimer = 0f;
        return true;
    }

    /// <summary>원거리 공격 키를 뗀 순간 호출</summary>
    public void ReleaseAttack()
    {
        if (_chargeState == ChargeState.Idle)
        {
            return;
        }

        int count = Mathf.Min(_activeCount, MaxActivatable);
        ResetCharge();

        if (count < 1)
        {
            return;
        }

        int fired = Fire(count);
        if (fired < 1)
        {
            return;
        }

        // 실제로 발사된 개수만큼만 soul 소모
        _soul.TryUseSoul(fired * _soulCostPerTalisman);
        OnFired?.Invoke(fired);
    }

    /// <summary>피격, 사망 등으로 발사 없이 충전을 취소할 때 호출</summary>
    public void CancelCharge() => ResetCharge();

    /// <summary>튜토리얼 연습 동안 충전 방식을 제한. 같은 owner가 Clear할 때까지 유지</summary>
    public void SetTutorialChargeMode(UnityEngine.Object owner, TutorialChargeMode mode)
    {
        if (owner == null)
        {
            return;
        }

        _tutorialChargeOwner = owner;
        _tutorialChargeMode = mode;
    }

    public void ClearTutorialChargeMode(UnityEngine.Object owner)
    {
        if (_tutorialChargeOwner != owner)
        {
            return;
        }

        _tutorialChargeOwner = null;
        _tutorialChargeMode = TutorialChargeMode.None;
    }

    /// <summary>버프로 데미지 증가량을 바꿀 때 사용</summary>
    public void SetDamageIncrease(float percent) => _damageIncreasePercent = percent;

    // ───────── 발사 ─────────

    // 실제로 발사된 개수를 반환
    private int Fire(int count)
    {
        int facing = GetFacing();
        int fired = 0;

        for (int i = 0; i < count; i++)
        {
            Transform slot = _slots[i].transform;

            // 슬롯 부적 숨김 처리
            _slots[i].gameObject.SetActive(false);

            if (ProjectilePool.Instance == null)
            {
                Debug.LogError("[PlayerRangedAttack] 씬에 ProjectilePool이 존재하지 않습니다.");
                break;
            }

            // 풀이 SetActive(true)와 OnSpawn까지 처리함
            Projectile p = ProjectilePool.Instance.GetProjectile(
                ProjectileType.Player, slot, transform, _damageIncreasePercent);

            if (p == null)
            {
                Debug.LogWarning("[PlayerRangedAttack] 풀에 남은 투사체가 없습니다.");
                break;
            }

            if (p is not PlayerProjectile projectile)
            {
                Debug.LogError("[PlayerRangedAttack] Player 타입 프리팹에 PlayerProjectile이 없습니다.");
                p.gameObject.SetActive(false);
                break;
            }

            projectile.SetFacing(facing);
            projectile.SetTrajectory(i, count);
            fired++;
        }

        return fired;
    }

    private void ActivateSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < _slotAnimators.Count)
        {
            Animator animator = _slotAnimators[slotIndex];
            if (animator != null && _slots[slotIndex].gameObject.activeSelf)
            {
                animator.ResetTrigger(AnimParamCancel);
                animator.SetTrigger(AnimParamActivate);
            }

            _slotFeedbacks[slotIndex]?.PlayActivation();
            OnChargeStep?.Invoke(slotIndex + 1);
        }
    }

    private void CancelAllActiveSlots()
    {
        for (int i = 0; i < _slotAnimators.Count; i++)
        {
            Animator animator = _slotAnimators[i];
            if (animator != null && _slots[i].gameObject.activeSelf)
            {
                animator.ResetTrigger(AnimParamActivate);
                animator.SetTrigger(AnimParamCancel);
            }

            _slotFeedbacks[i]?.StopActivation();
        }
    }

    private void ResetCharge()
    {
        if (_chargeState != ChargeState.Idle) OnChargeEnded?.Invoke();
        if (_chargeState != ChargeState.Idle && _activeCount > 0)
        {
            CancelAllActiveSlots();
        }

        _chargeState = ChargeState.Idle;
        _activeCount = 0;
        _chargeTimer = 0f;
    }

    private float CalculateChargeInterval()
    {
        float progress = Mathf.InverseLerp(1f, RequiredSlotCount - 1f, _activeCount);
        float minimumInterval = Mathf.Min(_chargeInterval, _minimumChargeInterval);
        return Mathf.Lerp(_chargeInterval, minimumInterval, progress);
    }

    // ───────── 슬롯 ─────────

    private void InitializeSlots()
    {
        _slots.Clear();
        _slotAnimators.Clear();
        _slotFeedbacks.Clear();

        if (_slotRoot == null)
        {
            Debug.LogWarning("[PlayerRangedAttack] 부적 슬롯 부모가 연결되지 않았습니다.", this);
            return;
        }

        for (int i = 0; i < _slotRoot.childCount; i++)
        {
            Transform slotTransform = _slotRoot.GetChild(i);
            SpriteRenderer slot = slotTransform.GetComponentInChildren<SpriteRenderer>(true);
            if (slot == null)
            {
                Debug.LogWarning($"[PlayerRangedAttack] {slotTransform.name}에 SpriteRenderer가 없습니다.", slotTransform);
                continue;
            }

            Animator slotAnimator = slot.GetComponent<Animator>();
            TalismanSlotVisualFeedback slotFeedback = slot.GetComponent<TalismanSlotVisualFeedback>();
            if (slotFeedback == null)
            {
                Debug.LogWarning($"[PlayerRangedAttack] {slotTransform.name}에 TalismanSlotVisualFeedback이 없습니다.", slotTransform);
            }

            slot.gameObject.SetActive(false);
            _slots.Add(slot);
            _slotAnimators.Add(slotAnimator);
            _slotFeedbacks.Add(slotFeedback);
        }

        if (_slots.Count != RequiredSlotCount)
        {
            Debug.LogWarning($"[PlayerRangedAttack] 부적 슬롯이 {_slots.Count}개 연결되었습니다. {RequiredSlotCount}개가 필요합니다.", this);
        }
    }

    private void InitializeFollowController()
    {
        if (_slotRoot == null)
        {
            return;
        }

        TalismanFollowController followController = _slotRoot.GetComponent<TalismanFollowController>();
        if (followController == null)
        {
            Debug.LogWarning("[PlayerRangedAttack] TalismanFollowController가 없습니다.", _slotRoot);
            return;
        }

        followController.Initialize(transform);
    }

    private void RefreshSlotVisuals()
    {
        int owned = Mathf.Min(_slots.Count, Mathf.FloorToInt(_soul.CurrentSoul / _soulCostPerTalisman));

        for (int i = 0; i < _slots.Count; i++)
        {
            bool visible = i < owned;
            bool wasVisible = _slots[i].gameObject.activeSelf;

            if (wasVisible != visible)
            {
                _slots[i].gameObject.SetActive(visible);
                if (visible)
                {
                    PlaySlotSpawnAnimation(i);
                }
            }
        }
    }

    private void PlaySlotSpawnAnimation(int slotIndex)
    {
        Animator animator = _slotAnimators[slotIndex];
        if (animator == null)
        {
            return;
        }

        animator.Play(AnimStateSpawn, 0, 0f);
    }

    // ───────── 공용 ─────────

    // scale.x가 음수면 왼쪽, 아니면 오른쪽
    private int GetFacing() => _flipTarget.localScale.x < 0f ? -1 : 1;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!_showFireRange || _rangePreviewProjectile == null)
        {
            return;
        }
        GameObject selected = UnityEditor.Selection.activeGameObject;
        if (selected == null || (selected != gameObject && !selected.transform.IsChildOf(transform)))
        {
            return;
        }

        // 실제 투사체의 탐색 설정을 읽어 표시와 발사 범위가 따로 바뀌지 않게 한다.
        Vector3 center = transform.position + (Vector3)_rangePreviewProjectile.SightOffset;
        Color previousColor = UnityEditor.Handles.color;
        UnityEditor.Handles.color = Color.cyan;
        UnityEditor.Handles.DrawWireDisc(center, Vector3.forward, _rangePreviewProjectile.SearchRadius);
        UnityEditor.Handles.color = previousColor;
    }
#endif
}
