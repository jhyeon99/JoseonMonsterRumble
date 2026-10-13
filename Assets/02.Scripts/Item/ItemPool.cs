using System.Collections;
using UnityEngine;

public class ItemPool : MonoBehaviour
{
    public static event System.Action<SoulItem[]> OnDeathSoulsSpawned;
    public static event System.Action<EnemyHealth, SoulItem[]> OnEnemyDeathSoulsSpawned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSoulSpawnListeners()
    {
        OnDeathSoulsSpawned = null;
        OnEnemyDeathSoulsSpawned = null;
    }
    private const float DeathSoulMinimumScatterSpeed = 10f;
    private const float DeathSoulMaximumScatterSpeed = 14f;
    private const float DeathSoulMinimumScatterTime = 0.65f;
    private const float DeathSoulMaximumScatterTime = 1f;
    private const float FullCircleRadians = Mathf.PI * 2f;
    private const float DeathSoulPositionJitterMultiplier = 0.3f;

    private static ItemPool _instance = null;
    public static ItemPool Instance
    {
        get
        {
            if (_instance == null)
            {
                ItemPool existing = FindFirstObjectByType<ItemPool>();
                if (existing != null)
                {
                    _instance = existing;
                }
                else
                {
                    ItemPool prefab = Resources.Load<ItemPool>("PF_ItemPool");
                    if (prefab != null)
                    {
                        _instance = Instantiate(prefab);
                        _instance.name = "ItemPool";
                    }
                }
            }
            return _instance;
        }
    }

    [Header("아이템 프리팹")]
    [SerializeField] private PotionItem _potionPrefab;
    [SerializeField] private SoulItem _soulPrefab;

    [Header("풀 사이즈")]
    [SerializeField] private int _potionPoolSize = 10;
    [SerializeField] private int _soulPoolSize = 50;

    // 소울의 네 가지 외형색: 적, 청, 녹, 황. 색은 획득량에 영향을 주지 않는다.
    private static readonly Color[] SoulColors = new Color[]
    {
        new Color(0.78f, 0.16f, 0.08f, 1f), // 주홍 계열
        new Color(0.12f, 0.3f, 0.65f, 1f),  // 짙은 청색 계열
        new Color(0.1f, 0.52f, 0.28f, 1f),  // 깊은 녹색 계열
        new Color(0.95f, 0.63f, 0.07f, 1f), // 황토빛 황색 계열
    };

    private PotionItem[] _potionPool;
    private SoulItem[] _soulPool;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        _ = Instance;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        InitPool();
    }

    private void OnEnable()
    {
        if (_instance != this) return;

        GameEvents.OnEnemyHit += HandleEnemyHit;
    }

    private void OnDisable()
    {
        GameEvents.OnEnemyHit -= HandleEnemyHit;
    }

    private void HandleEnemyHit(HitData hitData)
    {
        SpawnHitSoul(hitData.HitPoint, hitData.IsHeavyImpact);
    }

    private void InitPool()
    {
        _potionPool = new PotionItem[_potionPoolSize];
        for (int i = 0; i < _potionPoolSize; i++)
        {
            PotionItem potionItem = Instantiate(_potionPrefab, gameObject.transform);
            potionItem.gameObject.SetActive(false);
            _potionPool[i] = potionItem;
        }

        _soulPool = new SoulItem[_soulPoolSize];
        for (int i = 0; i < _soulPoolSize; i++)
        {
            SoulItem soulItem = Instantiate(_soulPrefab, gameObject.transform);
            soulItem.gameObject.SetActive(false);
            _soulPool[i] = soulItem;
        }
    }

    public PotionItem GetPotionItem()
    {
        for (int i = 0; i < _potionPool.Length; i++)
        {
            PotionItem potionItem = _potionPool[i];

            if (potionItem.gameObject.activeSelf == false)
            {
                potionItem.gameObject.SetActive(true);
                potionItem.OnSpawn();
                return potionItem;
            }
        }

        return null;
    }

    public SoulItem GetSoulItem()
    {
        for (int i = 0; i < _soulPool.Length; i++)
        {
            SoulItem soulItem = _soulPool[i];

            if (soulItem.gameObject.activeSelf == false)
            {
                soulItem.gameObject.SetActive(true);
                soulItem.OnSpawn();
                return soulItem;
            }
        }

        return null;
    }

    public SoulItem SpawnCustomSoul(Vector2 position, float amount, Color color, float scaleMultiplier, Vector2 initialVelocity, float waitTime = 0.35f)
    {
        for (int i = 0; i < _soulPool.Length; i++)
        {
            SoulItem soulItem = _soulPool[i];

            if (soulItem.gameObject.activeSelf == false)
            {
                soulItem.transform.position = position;
                soulItem.gameObject.SetActive(true);
                soulItem.Setup(amount, color, scaleMultiplier, initialVelocity, waitTime);
                return soulItem;
            }
        }

        // 풀이 모자라면 (보스 처치처럼 한 번에 많이 나올 때) 늘려서 사용
        SoulItem extra = ExpandSoulPool();
        extra.transform.position = position;
        extra.gameObject.SetActive(true);
        extra.Setup(amount, color, scaleMultiplier, initialVelocity, waitTime);
        return extra;
    }

    private SoulItem ExpandSoulPool()
    {
        SoulItem soulItem = Instantiate(_soulPrefab, gameObject.transform);
        soulItem.gameObject.SetActive(false);

        var expanded = new SoulItem[_soulPool.Length + 1];
        _soulPool.CopyTo(expanded, 0);
        expanded[_soulPool.Length] = soulItem;
        _soulPool = expanded;
        return soulItem;
    }

    public void SpawnHitSoul(Vector2 hitPosition, bool isHeavy)
    {
        float amount = isHeavy ? 0.35f : 0.15f;
        float scale = isHeavy ? 1.85f : 0.95f;
        Color color = GetRandomSoulColor();

        Vector2 randomDir = (Vector2.up * 1.5f + Random.insideUnitCircle * 1.2f).normalized;
        Vector2 velocity = randomDir * (isHeavy ? Random.Range(3.6f, 5.2f) : Random.Range(2.5f, 4.0f));
        float waitTime = isHeavy ? Random.Range(0.12f, 0.18f) : Random.Range(0.08f, 0.14f);

        SpawnCustomSoul(hitPosition, amount, color, scale, velocity, waitTime);
    }

    public void SpawnDeathSouls(Vector2 centerPosition, int count = 5, EnemyHealth source = null)
    {
        SpawnDeathSouls(centerPosition, count, 1f, 1f, source);
    }

    /// <summary>
    /// 사망 소울 (개별 무작위 방향으로 퍼짐). totalAmount = 전체 소울 충전량 (1 = 슬롯 1칸),
    /// speedMultiplier = 퍼지는 속도 배율 (보스처럼 많이 뿌릴 때 크게)
    /// </summary>
    public void SpawnDeathSouls(Vector2 centerPosition, int count, float totalAmount, float speedMultiplier,
        EnemyHealth source = null)
    {
        SpawnDeathSoulScatter(centerPosition, count, totalAmount / Mathf.Max(1, count), speedMultiplier, 0f, source);
    }

    /// <summary>
    /// 사망 소울을 여러 번 나눠서 뿌림 (perBurst개씩 bursts번, interval초 간격).
    /// totalAmount = 전체 소울 충전량, spawnRadius = 생성 위치가 흩어지는 반경 (회차마다 중심이 이 안에서 무작위).
    /// 오브젝트가 꺼져도(보스 처치 등) ItemPool에서 계속 진행됨
    /// </summary>
    public void SpawnDeathSoulBursts(
        Vector2 centerPosition,
        int perBurst,
        int bursts,
        float interval,
        float totalAmount,
        float speedMultiplier,
        float spawnRadius = 0f
    )
    {
        perBurst = Mathf.Max(1, perBurst);
        bursts = Mathf.Max(1, bursts);
        StartCoroutine(DeathSoulBurstRoutine(centerPosition, perBurst, bursts, interval, totalAmount, speedMultiplier, spawnRadius));
    }

    private IEnumerator DeathSoulBurstRoutine(
        Vector2 centerPosition,
        int perBurst,
        int bursts,
        float interval,
        float totalAmount,
        float speedMultiplier,
        float spawnRadius
    )
    {
        float amountPerSoul = totalAmount / (perBurst * bursts);

        for (int burst = 0; burst < bursts; burst++)
        {
            // 회차마다 터지는 중심을 반경 안에서 무작위로
            Vector2 burstCenter = centerPosition + (Random.insideUnitCircle * spawnRadius);
            SpawnDeathSoulScatter(burstCenter, perBurst, amountPerSoul, speedMultiplier,
                spawnRadius * DeathSoulPositionJitterMultiplier);

            if (burst < bursts - 1 && interval > 0f)
            {
                yield return new WaitForSeconds(interval);
            }
        }
    }

    /// <param name="jitterRadius">소울마다 생성 위치를 이 반경 안에서 살짝 흩뜨림</param>
    private void SpawnDeathSoulScatter(
        Vector2 centerPosition,
        int count,
        float amountPerSoul,
        float speedMultiplier,
        float jitterRadius,
        EnemyHealth source = null
    )
    {
        count = Mathf.Max(1, count);

        // Only allocate a snapshot when a tutorial/observer needs the actual death drops.
        SoulItem[] spawned = OnDeathSoulsSpawned != null || source != null && OnEnemyDeathSoulsSpawned != null
            ? new SoulItem[count] : null;

        for (int i = 0; i < count; i++)
        {
            Color color = SoulColors[i % SoulColors.Length];
            float rad = Random.Range(0f, FullCircleRadians);
            var direction = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 velocity = direction * (Random.Range(DeathSoulMinimumScatterSpeed, DeathSoulMaximumScatterSpeed) * speedMultiplier);

            float waitTime = Random.Range(DeathSoulMinimumScatterTime, DeathSoulMaximumScatterTime);
            Vector2 spawnPosition = centerPosition + (Random.insideUnitCircle * jitterRadius);
            SoulItem soulItem = SpawnCustomSoul(spawnPosition, amountPerSoul, color, 1.15f, velocity, waitTime);
            soulItem.EnableDeathMotion();
            if (spawned != null) spawned[i] = soulItem;
        }
        if (spawned != null)
        {
            if (source != null) OnEnemyDeathSoulsSpawned?.Invoke(source, spawned);
            OnDeathSoulsSpawned?.Invoke(spawned);
        }
    }

    public Color GetRandomSoulColor()
    {
        return SoulColors[Random.Range(0, SoulColors.Length)];
    }
}
