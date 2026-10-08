using System.Collections;
using UnityEngine;

/// <summary>
/// 침뱉기. 애니메이션 길이 대비 발사 비율마다 한 발씩 발사.
/// 기존 적 투사체(ProjectilePool)를 그대로 사용하므로 패링하면 보스 입 쪽으로 반사됨.
/// 씬의 ProjectilePool에 해당 타입 투사체 프리팹이 등록되어 있어야 함.
/// (판정 시작 / 끝 비율은 사용하지 않음)
/// </summary>
public class BossSpitPattern : BossPattern
{
    [Header("침뱉기")]
    [Tooltip("ProjectilePool에 등록된 투사체 타입 (Flying = 직선, Ground = 곡사)")]
    [SerializeField]
    private ProjectileType _projectileType = ProjectileType.Flying;

    [Tooltip("발사 위치 (입). 반사된 투사체도 이쪽으로 돌아옴")]
    [SerializeField]
    private Transform _muzzle;

    [Tooltip("발사 지점들 (애니메이션 길이 대비 비율). 3발이면 0.4, 0.55, 0.7 처럼 입력")]
    [SerializeField]
    private float[] _fireRatios = { 0.5f };

    [Tooltip("투사체 데미지 증가율 (%)")]
    [SerializeField]
    private float _damageIncreasePercent = 0f;

    private void Reset()
    {
        SetDefaults("침뱉기", BossPhaseMask.Both, BossFloorMask.Any);
    }

    protected override void OnInitialize()
    {
        if (_muzzle == null)
        {
            _muzzle = transform;
        }

        if (_fireRatios != null)
        {
            System.Array.Sort(_fireRatios);
        }
    }

    protected override IEnumerator Execute()
    {
        if (_fireRatios == null)
        {
            yield break;
        }

        foreach (float ratio in _fireRatios)
        {
            yield return WaitUntilProgress(Mathf.Clamp01(ratio));
            Fire();
        }
    }

    private void Fire()
    {
        if (ProjectilePool.Instance == null || Boss.Player == null)
        {
            Debug.LogWarning($"{name}: ProjectilePool 또는 플레이어가 없어 침뱉기를 건너뜁니다.", this);
            return;
        }

        Projectile projectile = ProjectilePool.Instance.GetProjectile(
            _projectileType,
            _muzzle,
            Boss.Player,
            _damageIncreasePercent
        );

        if (projectile == null)
        {
            Debug.LogWarning($"{name}: ProjectilePool에 {_projectileType} 투사체가 없거나 모두 사용 중입니다.", this);
            return;
        }

        projectile.transform.position = _muzzle.position;

        // 직선 투사체는 로컬 왼쪽(-right)으로 날아가므로 플레이어 방향 + 180도
        Vector2 toPlayer = (Vector2)Boss.Player.position - (Vector2)_muzzle.position;
        float angle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg + 180f;
        projectile.transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}
