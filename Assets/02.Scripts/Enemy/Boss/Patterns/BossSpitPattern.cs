using System.Collections;
using UnityEngine;

/// <summary>
/// 침뱉기. 한 번 뱉는 애니메이션을 개수 배열 길이만큼 반복 재생하고, 매번 발사 비율 지점에서 배열 값만큼 뱉음.
/// 예: Shot Counts = 3, 5, 3 → 애니메이션 3번 재생, 3개 / 5개 / 3개 발사.
/// 직선 투사체는 플레이어 방향 기준 부채꼴로 퍼지고, 발사 간격을 주면 한 개씩 연달아 나감.
/// 기존 적 투사체(ProjectilePool)를 그대로 사용하므로 패링하면 보스 입 쪽으로 반사됨.
/// 씬의 ProjectilePool에 해당 타입 투사체 프리팹이 등록되어 있어야 함.
/// (판정 시작 / 끝 비율은 사용하지 않음)
/// </summary>
public class BossSpitPattern : BossPattern
{
    [Header("침뱉기")]
    [Tooltip("ProjectilePool에 등록된 투사체 타입 (BossSpit = 보스 침 전용 직선, Flying = 직선, Ground = 곡사)")]
    [SerializeField]
    private ProjectileType _projectileType = ProjectileType.BossSpit;

    [Tooltip("발사 위치 (입). 반사된 투사체도 이쪽으로 돌아옴")]
    [SerializeField]
    private Transform _muzzle;

    [Tooltip("뱉을 때마다 한 번에 나가는 개수. 원소 수 = 뱉는 횟수 (애니메이션을 그 횟수만큼 반복 재생). 비어 있으면 1번 1개")]
    [SerializeField]
    private int[] _shotCounts = { 3, 5, 3 };

    [Tooltip("한 번 뱉는 애니메이션에서 실제로 발사하는 지점 (애니메이션 길이 대비 비율)")]
    [SerializeField, Range(0f, 1f)]
    private float _fireRatio = 0.5f;

    [Tooltip("마지막이 아닌 뱉기에서 다음 뱉기 애니메이션을 시작하는 지점 (비율). 1 = 애니메이션이 끝난 뒤")]
    [SerializeField, Range(0f, 1f)]
    private float _nextShotRatio = 1f;

    [Tooltip("뱉기 사이 추가 대기 (초)")]
    [SerializeField, Min(0f)]
    private float _delayBetweenShots = 0f;

    [Tooltip("한 번에 여러 개를 뱉을 때 퍼지는 전체 각도 (도). 직선(BossSpit / Flying) 투사체만 적용, 곡사(Ground)는 모두 플레이어 위치로 떨어짐")]
    [SerializeField, Range(0f, 180f)]
    private float _spreadAngle = 30f;

    [Tooltip("한 번에 여러 개를 뱉을 때 하나씩 나가는 간격 (초). 0이면 동시에")]
    [SerializeField, Min(0f)]
    private float _projectileInterval = 0f;

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

    }

    protected override IEnumerator Execute()
    {
        int shots = _shotCounts != null && _shotCounts.Length > 0 ? _shotCounts.Length : 1;
        float nextShotRatio = Mathf.Max(_nextShotRatio, _fireRatio);

        for (int shot = 0; shot < shots; shot++)
        {
            if (shot > 0)
            {
                if (_delayBetweenShots > 0f)
                {
                    yield return new WaitForSeconds(_delayBetweenShots);
                }

                // 한 번 뱉는 애니메이션을 처음부터 다시 재생 (진행도도 0부터)
                Boss.FacePlayer();
                ReplayAnimation();
            }

            yield return WaitUntilProgress(_fireRatio);

            int count = _shotCounts != null && shot < _shotCounts.Length ? Mathf.Max(1, _shotCounts[shot]) : 1;
            for (int i = 0; i < count; i++)
            {
                Fire(GetSpreadOffset(i, count));

                if (_projectileInterval > 0f && i < count - 1)
                {
                    yield return new WaitForSeconds(_projectileInterval);
                }
            }

            if (shot < shots - 1)
            {
                yield return WaitUntilProgress(nextShotRatio);
            }
        }
    }

    /// <summary>부채꼴에서 index번째 투사체의 각도 (가운데가 플레이어 방향)</summary>
    private float GetSpreadOffset(int index, int count)
    {
        // 곡사(Ground)는 모두 플레이어 위치로 떨어지므로 퍼뜨리지 않음 (직선 / 보스 침은 퍼짐)
        if (count <= 1 || _projectileType == ProjectileType.Ground)
        {
            return 0f;
        }

        return Mathf.Lerp(-_spreadAngle * 0.5f, _spreadAngle * 0.5f, index / (float)(count - 1));
    }

    private void Fire(float angleOffset)
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
        float angle = (Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg) + 180f + angleOffset;
        projectile.transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}
