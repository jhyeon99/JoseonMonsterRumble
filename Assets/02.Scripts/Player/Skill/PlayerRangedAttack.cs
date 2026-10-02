using System;
using UnityEngine;

public class PlayerRangedAttack : MonoBehaviour
{
    [Header("발사")]
    [SerializeField] private Transform _muzzle;

    [SerializeField] private float _cooldown = 0.5f;

    [Header("방향")]
    [SerializeField] private Transform _flipTarget;

    [Header("데미지")]
    [SerializeField] private float _damageIncreasePercent = 0f; // 버프 등으로 증가할 % (0 = 기본 데미지)

    private float _lastFireTime = float.NegativeInfinity;

    public bool IsReady => Time.time >= _lastFireTime + _cooldown;
    public float CooldownRemaining => Mathf.Max(0f, _lastFireTime + _cooldown - Time.time);

    /// <summary>발사 성공 시 호출 (사운드, 이펙트, 영혼 소모 등 연결용)</summary>
    public event Action OnFired;

    private void Awake()
    {
        if (_flipTarget == null) _flipTarget = transform;
    }

    /// <summary>입력 처리나 상태에서 호출. 쿨타임 중이면 false</summary>
    public bool TryFire()
    {
        if (!IsReady) return false;

        PlayerProjectile projectile = GetProjectileFromPool();
        if (projectile == null)
        {
            Debug.LogWarning("[PlayerRangedAttack] 풀에서 투사체를 가져오지 못했습니다.");
            return false;
        }

        projectile.transform.position = _muzzle.position;
        projectile.gameObject.SetActive(true);
        projectile.SetFacing(GetFacing());
        projectile.OnSpawn(_muzzle, transform, _damageIncreasePercent);

        _lastFireTime = Time.time;
        OnFired?.Invoke();
        return true;
    }

    /// <summary>버프로 데미지 증가량을 바꿀 때 사용</summary>
    public void SetDamageIncrease(float percent) => _damageIncreasePercent = percent;

    // scale.x가 음수면 왼쪽, 아니면 오른쪽
    private int GetFacing() => _flipTarget.localScale.x < 0f ? -1 : 1;

    // ───────── 공용 풀 연결부: 실제 ProjectilePool API에 맞게 이 함수만 수정 ─────────
    private PlayerProjectile GetProjectileFromPool()
    {
        Projectile p = ProjectilePool.Instance.GetProjectile(ProjectileType.Player, _muzzle.transform, transform,
            _damageIncreasePercent);
        return p as PlayerProjectile;
    }
}