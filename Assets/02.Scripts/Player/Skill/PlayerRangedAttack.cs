using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerRangedAttack : MonoBehaviour
{
    [SerializeField] private Transform _flipTarget;

    [Header("부적 배치")]
    [SerializeField] private SpriteRenderer _talismanPrefab; // 머리 위에 표시할 부적 스프라이트

    [SerializeField] private int _slotCount = 5;

    [SerializeField] private Vector2 _centerOffset = new Vector2(0f, 1.2f); // 플레이어 피벗 기준 부채꼴 중심

    [SerializeField] private float _radius = 0.8f;

    [SerializeField] private float _spreadAngle = 120f; // 부채꼴 전체 각도

    [Header("표시")]
    [SerializeField] private Color _idleColor = new Color(1f, 1f, 1f, 0.35f); // 보유 중(비활성)

    [SerializeField] private Color _activeColor = Color.white; // 활성화됨

    [Header("충전")]
    [SerializeField] private float _chargeInterval = 0.4f; // 누르고 있을 때 부적이 하나씩 추가되는 간격

    [SerializeField] private float _soulCostPerTalisman = 1f;

    [Header("데미지")]
    [SerializeField] private float _damageIncreasePercent = 0f;

    private readonly List<SpriteRenderer> _slots = new List<SpriteRenderer>();
    private Transform _slotRoot;

    private PlayerSoul _soul;

    private bool _isCharging;
    private int _activeCount;
    private float _chargeTimer;

    public bool IsCharging => _isCharging;
    public int ActiveCount => _activeCount;

    /// <summary>발사 시 호출. 인자 = 실제로 발사된 부적 개수 (사운드, 이펙트 연결용)</summary>
    public event Action<int> OnFired;

    // 지금 활성화할 수 있는 최대 개수 (슬롯 수와 보유 soul 중 작은 값)
    private int MaxActivatable =>
        Mathf.Min(_slotCount, Mathf.FloorToInt(_soul.CurrentSoul / _soulCostPerTalisman));

    private void Awake()
    {
        _soul = GetComponent<PlayerSoul>();
        if (_flipTarget == null)
        {
            _flipTarget = transform;
        }

        CreateSlots();
    }

    private void Update()
    {
        if (!_isCharging)
        {
            return;
        }

        _chargeTimer += Time.deltaTime;
        while (_chargeTimer >= _chargeInterval)
        {
            _chargeTimer -= _chargeInterval;
            if (_activeCount < MaxActivatable) _activeCount++;
        }
    }

    private void LateUpdate()
    {
        // 플레이어 좌우 반전에 영향받지 않도록 슬롯은 자식이 아닌 별도 오브젝트로 두고 위치만 따라감
        _slotRoot.position = transform.position + (Vector3)_centerOffset;
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
        if (_isCharging) return;
        if (MaxActivatable < 1) return; // soul 부족

        _isCharging = true;
        _activeCount = 1; // 누르자마자 1개 (단타)
        _chargeTimer = 0f;
    }

    /// <summary>원거리 공격 키를 뗀 순간 호출</summary>
    public void ReleaseAttack()
    {
        if (!_isCharging) return;

        int count = Mathf.Min(_activeCount, MaxActivatable);
        ResetCharge();

        if (count < 1) return;

        int fired = Fire(count);
        if (fired < 1) return;

        // 실제로 발사된 개수만큼만 soul 소모
        _soul.TryUseSoul(fired * _soulCostPerTalisman);
        OnFired?.Invoke(fired);
    }

    /// <summary>피격, 사망 등으로 발사 없이 충전을 취소할 때 호출</summary>
    public void CancelCharge() => ResetCharge();

    /// <summary>스펠(단발 원거리 공격) 시전 가능 여부 (영혼 보유 확인)</summary>
    public bool CanCastSpell => _soul != null && _soul.CurrentSoul >= _soulCostPerTalisman;

    /// <summary>스펠 애니메이션 시점(PlayerSpellState)에서 단발 부적 발사</summary>
    public bool FireSingleSpell()
    {
        if (!CanCastSpell)
        {
            return false;
        }

        int fired = Fire(1);
        if (fired < 1)
        {
            return false;
        }

        _soul.TryUseSoul(fired * _soulCostPerTalisman);
        OnFired?.Invoke(fired);
        return true;
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

            // 풀은 위치를 옮겨주지 않으므로 직접 지정
            projectile.transform.position = slot.position;
            projectile.SetFacing(facing);
            fired++;
        }

        return fired;
    }

    private void ResetCharge()
    {
        _isCharging = false;
        _activeCount = 0;
        _chargeTimer = 0f;
    }

    // ───────── 슬롯 ─────────

    private void CreateSlots()
    {
        _slotRoot = new GameObject($"{name}_TalismanSlots").transform;

        for (int i = 0; i < _slotCount; i++)
        {
            // 왼쪽(i = 0)부터 오른쪽 순서로 부채꼴 배치
            float t = _slotCount == 1 ? 0.5f : (float)i / (_slotCount - 1);
            float angle = 90f + _spreadAngle * 0.5f - _spreadAngle * t;
            float rad = angle * Mathf.Deg2Rad;

            SpriteRenderer slot = Instantiate(_talismanPrefab, _slotRoot);
            slot.transform.localPosition = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * _radius;
            slot.transform.localRotation = Quaternion.Euler(0f, 0f, angle - 90f); // 바깥쪽을 향하게
            slot.gameObject.SetActive(false);

            _slots.Add(slot);
        }
    }

    private void RefreshSlotVisuals()
    {
        int owned = Mathf.Min(_slotCount, Mathf.FloorToInt(_soul.CurrentSoul / _soulCostPerTalisman));

        for (int i = 0; i < _slots.Count; i++)
        {
            bool visible = i < owned;
            if (_slots[i].gameObject.activeSelf != visible)
                _slots[i].gameObject.SetActive(visible);

            if (visible)
                _slots[i].color = i < _activeCount ? _activeColor : _idleColor;
        }
    }

    // ───────── 공용 ─────────

    // scale.x가 음수면 왼쪽, 아니면 오른쪽
    private int GetFacing() => _flipTarget.localScale.x < 0f ? -1 : 1;
}