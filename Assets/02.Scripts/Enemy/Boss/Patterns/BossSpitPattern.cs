using System.Collections;
using UnityEngine;

/// <summary>
/// 침뱉기. 뱉는 횟수를 가중치로 고르고, 그 횟수만큼 한 번 뱉는 애니메이션을 반복 재생하며 매번 발사 비율 지점에서 n ~ m개를 뱉음.
/// 예: Spit Count Weights = 0.2, 0.5, 0.3 / Projectiles Per Spit = 3 ~ 5 → 20% 1번 / 50% 2번 / 30% 3번 뱉고, 매번 3 ~ 5개 발사.
/// 보스 침(BossSpit)은 중력을 받는 포물선으로 날아가며, 플레이어의 움직임을 어느 정도 예측하되
/// 착지 지점 / 비행 시간 / 발사 위치에 랜덤을 섞어 자연스럽게 흩뿌림 (일부는 크게 빗나가는 견제탄).
/// 그 외 타입(Flying 등)은 기존처럼 플레이어 방향 기준 부채꼴로 직선 발사.
/// 기존 적 투사체(ProjectilePool)를 그대로 사용하므로 패링하면 보스 입 쪽으로 반사됨.
/// 씬의 ProjectilePool에 해당 타입 투사체 프리팹이 등록되어 있어야 함.
/// (판정 시작 / 끝 비율은 사용하지 않음)
/// </summary>
public class BossSpitPattern : BossPattern
{
    [Header("침뱉기")]
    [Tooltip("ProjectilePool에 등록된 투사체 타입 (BossSpit = 보스 침 전용 포물선, Flying = 직선, Ground = 곡사)")]
    [SerializeField]
    private ProjectileType _projectileType = ProjectileType.BossSpit;

    [Tooltip("발사 위치 (입). 반사된 투사체도 이쪽으로 돌아옴")]
    [SerializeField]
    private Transform _muzzle;

    [Tooltip("뱉는 횟수별 가중치. 원소 순서 = 1번, 2번, 3번... 뱉을 확률 (예: 0.2, 0.5, 0.3이면 20% 1번 / 50% 2번 / 30% 3번). 최대 횟수 = 배열 크기. 비어 있으면 1번")]
    [SerializeField]
    private float[] _spitCountWeights = { 0.2f, 0.5f, 0.3f };

    [Tooltip("한 번 뱉을 때 나가는 침 개수 (최소 ~ 최대, 뱉을 때마다 랜덤)")]
    [SerializeField]
    private Vector2Int _projectilesPerSpit = new Vector2Int(3, 5);

    [Tooltip("켜면 한 번 뱉을 때 나오는 침 중 무작위로 1개만 패링 가능 (반짝임, 패링하면 보스 쪽으로 반사). 나머지는 막기만 가능")]
    [SerializeField]
    private bool _oneParryablePerSpit = true;

    [Tooltip("한 번 뱉는 애니메이션에서 뱉기를 시작하는 지점 (애니메이션 길이 대비 비율). 입 이펙트가 여기서 나오고, Spit Delay 뒤에 침이 나감")]
    [SerializeField, Range(0f, 1f)]
    private float _fireRatio = 0.5f;

    [Tooltip("마지막이 아닌 뱉기에서 다음 뱉기 애니메이션을 시작하는 지점 (비율). 1 = 애니메이션이 끝난 뒤")]
    [SerializeField, Range(0f, 1f)]
    private float _nextShotRatio = 1f;

    [Tooltip("뱉기 사이 추가 대기 (초)")]
    [SerializeField, Min(0f)]
    private float _delayBetweenShots = 0f;

    [Tooltip("BossSpit이 아닌 직선(Flying) 투사체를 쓸 때 퍼지는 전체 각도 (도). 곡사(Ground)는 모두 플레이어 위치로 떨어짐")]
    [SerializeField, Range(0f, 180f)]
    private float _spreadAngle = 30f;

    [Tooltip("한 번에 여러 개를 뱉을 때 하나씩 나가는 간격 (초). 0이면 동시에")]
    [SerializeField, Min(0f)]
    private float _projectileInterval = 0f;

    [Tooltip("발사 간격에 더해지는 랜덤 (초, 0 ~ 이 값). 같은 뱉기 안에서도 박자가 들쭉날쭉하게")]
    [SerializeField, Min(0f)]
    private float _intervalJitter = 0.04f;

    [Header("포물선 조준 (BossSpit 투사체)")]
    [Tooltip("침이 목표 지점까지 날아가는 시간 (초, 최소 ~ 최대에서 랜덤). 길수록 높이 솟았다 떨어짐")]
    [SerializeField]
    private Vector2 _flightTimeRange = new Vector2(1.6f, 2.4f);

    [Tooltip("플레이어 이동 예측 정도 (0 = 현재 위치, 1 = 도착 시점 위치를 정확히 예측)")]
    [SerializeField, Range(0f, 1f)]
    private float _leadFactor = 0.5f;

    [Tooltip("조준탄의 착지 지점 가로 흩어짐 (예측 위치 기준 ± 이 값)")]
    [SerializeField, Min(0f)]
    private float _aimSpreadX = 2f;

    [Tooltip("견제탄(크게 빗나가는 침)이 나올 확률 (0 ~ 1)")]
    [SerializeField, Range(0f, 1f)]
    private float _wildShotChance = 0.3f;

    [Tooltip("견제탄의 착지 지점 가로 흩어짐 (예측 위치 기준 ± 이 값)")]
    [SerializeField, Min(0f)]
    private float _wildSpreadX = 7f;

    [Tooltip("목표 높이 = 플레이어 위치 + 랜덤 (최소 ~ 최대)")]
    [SerializeField]
    private Vector2 _targetHeightRange = new Vector2(-0.5f, 0.5f);

    [Tooltip("발사 위치 랜덤 흔들림 (입 주변 반경)")]
    [SerializeField, Min(0f)]
    private float _muzzleJitter = 0.15f;

    [Tooltip("맵 양 끝에서 이만큼 안쪽으로만 조준")]
    [SerializeField, Min(0f)]
    private float _aimEdgeMargin = 0.5f;

    [Header("입 이펙트")]
    [Tooltip("켜면 뱉을 때마다 BossSpit 이펙트 재생 (EffectPool에 BossSpit 등록 필요)")]
    [SerializeField]
    private bool _playMouthVfx = true;

    [Tooltip("입 이펙트가 나오는 위치. 비어 있으면 발사 위치(Muzzle) 사용")]
    [SerializeField]
    private Transform _vfxPoint;

    [Tooltip("입 이펙트가 나온 뒤 침이 실제로 나가기까지 (초). 입 이펙트를 끄면 사용 안 함")]
    [SerializeField, Min(0f)]
    private float _spitDelay = 0.1f;

    [Tooltip("투사체 데미지 증가율 (%)")]
    [SerializeField]
    private float _damageIncreasePercent = 0f;

    private bool _bundleCueSent;

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
        int shots = PickSpitCount();
        Boss.Log($"{PatternName}: {shots}번 뱉기");
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

            // 입 이펙트 먼저 → Spit Delay 뒤에 침 발사
            if (_playMouthVfx)
            {
                PlayMouthVfx();

                if (_spitDelay > 0f)
                {
                    yield return new WaitForSeconds(_spitDelay);
                }
            }
            int min = Mathf.Max(1, _projectilesPerSpit.x);
            int max = Mathf.Max(min, _projectilesPerSpit.y);
            int count = Random.Range(min, max + 1);

            // 이번 뱉기에서 패링 가능한 침 1개를 무작위로 정함
            int parryableIndex = _oneParryablePerSpit ? Random.Range(0, count) : -1;
            for (int i = 0; i < count; i++)
            {
                Fire(GetSpreadOffset(i, count), i == parryableIndex);

                if (i < count - 1)
                {
                    float interval = _projectileInterval + Random.Range(0f, _intervalJitter);
                    if (interval > 0f)
                    {
                        yield return new WaitForSeconds(interval);
                    }
                }
            }

            if (shot < shots - 1)
            {
                yield return WaitUntilProgress(nextShotRatio);
            }
        }
    }

    private void PlayMouthVfx()
    {
        if (EffectManager.Instance != null)
        {
            Transform point = _vfxPoint != null ? _vfxPoint : _muzzle;
            EffectManager.Instance.PlayBossSpit(point.position, Boss.IsFacingRight);
        }
    }

    /// <summary>가중치 배열로 이번에 뱉을 횟수 결정 (1 ~ 배열 크기). 배열이 비었거나 합이 0이면 1번</summary>
    private int PickSpitCount()
    {
        if (_spitCountWeights == null || _spitCountWeights.Length == 0)
        {
            return 1;
        }

        float total = 0f;
        foreach (float weight in _spitCountWeights)
        {
            total += Mathf.Max(0f, weight);
        }

        if (total <= 0f)
        {
            return 1;
        }

        float pick = Random.value * total;
        for (int i = 0; i < _spitCountWeights.Length; i++)
        {
            pick -= Mathf.Max(0f, _spitCountWeights[i]);
            if (pick <= 0f && _spitCountWeights[i] > 0f)
            {
                return i + 1;
            }
        }

        // 부동소수점 오차로 끝까지 온 경우: 가중치가 있는 마지막 횟수
        for (int i = _spitCountWeights.Length - 1; i >= 0; i--)
        {
            if (_spitCountWeights[i] > 0f)
            {
                return i + 1;
            }
        }

        return 1;
    }

    /// <summary>
    /// 침이 flightTime초 뒤 지나갈 목표 지점.
    /// 플레이어 이동을 Lead Factor만큼 예측한 위치 기준으로, 대부분은 가까이(조준탄) 일부는 멀리(견제탄) 흩뿌림
    /// </summary>
    private Vector2 PickTarget(float flightTime)
    {
        Vector2 playerPosition = Boss.Player.position;

        Vector2 playerVelocity = Vector2.zero;
        Rigidbody2D playerBody = Boss.Player.GetComponent<Rigidbody2D>();
        if (playerBody != null)
        {
            // 점프 중 세로 속도까지 예측하면 엉뚱하게 높아지므로 가로만
            playerVelocity = new Vector2(playerBody.linearVelocity.x, 0f);
        }

        Vector2 predicted = playerPosition + (playerVelocity * (flightTime * _leadFactor));
        float spread = Random.value < _wildShotChance ? _wildSpreadX : _aimSpreadX;

        float x = predicted.x + Random.Range(-spread, spread);
        if (Boss.Arena != null)
        {
            x = Boss.Arena.ClampX(x, _aimEdgeMargin);
        }

        float y = playerPosition.y + Random.Range(_targetHeightRange.x, Mathf.Max(_targetHeightRange.x, _targetHeightRange.y));
        return new Vector2(x, y);
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

    private void Fire(float angleOffset, bool parryable)
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

        // 보스 침: 중력 포물선 + 랜덤 조준
        if (projectile is BossSpitProjectile spit)
        {
            Vector2 origin = (Vector2)_muzzle.position + (Random.insideUnitCircle * _muzzleJitter);
            float flightTime = Random.Range(_flightTimeRange.x, Mathf.Max(_flightTimeRange.x, _flightTimeRange.y));
            Vector2 target = PickTarget(flightTime);

            spit.transform.position = origin;
            spit.SetParryable(parryable);
            spit.Launch(BossSpitProjectile.CalculateVelocity(origin, target, flightTime, spit.Gravity));
            return;
        }

        projectile.transform.position = _muzzle.position;

        // 직선 투사체는 로컬 왼쪽(-right)으로 날아가므로 플레이어 방향 + 180도
        Vector2 toPlayer = (Vector2)Boss.Player.position - (Vector2)_muzzle.position;
        float angle = (Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg) + 180f + angleOffset;
        projectile.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        if (!_bundleCueSent)
        {
            _bundleCueSent = true;
            AudioResult("Boss.Spit", _muzzle.position);
        }
    }
}
