using System.Collections;
using UnityEngine;

/// <summary>
/// 침뱉기. 기존 적 투사체(ProjectilePool)를 그대로 사용하므로 패링하면 보스 입 쪽으로 반사됨.
/// 씬의 ProjectilePool에 해당 타입 투사체 프리팹이 등록되어 있어야 함.
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

    [SerializeField, Min(1)] private int _shotCount = 3;
    [SerializeField, Min(0f)] private float _windupTime = 0.5f;
    [SerializeField, Min(0f)] private float _shotInterval = 0.35f;
    [SerializeField, Min(0f)] private float _recoverTime = 0.6f;

    [Tooltip("투사체 데미지 증가율 (%)")]
    [SerializeField]
    private float _damageIncreasePercent = 0f;

    [Header("애니메이션 (없으면 무시)")]
    [SerializeField] private string _spitTrigger = "spit";

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
    }

    protected override IEnumerator Execute()
    {
        Boss.FacePlayer();

        yield return new WaitForSeconds(_windupTime);

        for (int i = 0; i < _shotCount; i++)
        {
            Boss.FacePlayer();
            Boss.SetTrigger(_spitTrigger);
            Fire();

            if (i < _shotCount - 1)
            {
                yield return new WaitForSeconds(_shotInterval);
            }
        }

        yield return new WaitForSeconds(_recoverTime);
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
