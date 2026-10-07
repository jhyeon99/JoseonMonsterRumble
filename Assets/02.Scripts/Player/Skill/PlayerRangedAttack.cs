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

    private const int RequiredSlotCount = 5;

    [SerializeField] private Transform _flipTarget;

    [Header("부적 배치")]
    [SerializeField] private Transform _slotRoot;

    [Header("충전")]
    [SerializeField, Min(0.01f)] private float _chargeInterval = 0.4f; // 누르고 있을 때 부적이 하나씩 추가되는 간격

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

    public bool IsCharging => _chargeState == ChargeState.Charging;
    public int ActiveCount => _activeCount;

    /// <summary>발사 시 호출. 인자 = 실제로 발사된 부적 개수 (사운드, 이펙트 연결용)</summary>
    public event Action<int> OnFired;

    // 지금 활성화할 수 있는 최대 개수 (슬롯 수와 보유 soul 중 작은 값)
    private int MaxActivatable =>
        Mathf.Min(_slots.Count, Mathf.FloorToInt(_soul.CurrentSoul / _soulCostPerTalisman));

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
        while (_chargeTimer >= _chargeInterval)
        {
            _chargeTimer -= _chargeInterval;
            if (_activeCount < MaxActivatable)
            {
                ActivateSlot(_activeCount);
                _activeCount++;
            }
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

    /// <summary>스펠(단발 원거리 공격) 시전 가능 여부 (영혼 보유 확인)</summary>
    public bool CanCastSpell => _soul != null && _soul.CurrentSoul >= _soulCostPerTalisman;

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
        if (_chargeState != ChargeState.Idle && _activeCount > 0)
        {
            CancelAllActiveSlots();
        }

        _chargeState = ChargeState.Idle;
        _activeCount = 0;
        _chargeTimer = 0f;
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
}
