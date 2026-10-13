using System.Collections;
using UnityEngine;

public class ItemPool : MonoBehaviour
{
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

    // 동양풍 영기 톤 (청백, 벽청, 연금빛, 비취, 연자주)
    private static readonly Color[] SoulColors = new Color[]
    {
        new Color(0.65f, 0.88f, 1f, 1f),    // 청백색 (은은한 영혼 불빛)
        new Color(0.25f, 0.65f, 0.95f, 1f), // 벽청색 (깊은 푸른 영기)
        new Color(1f, 0.88f, 0.45f, 1f),    // 연금빛 (신비로운 황금빛)
        new Color(0.45f, 0.95f, 0.75f, 1f), // 비취색 (옥빛 영혼)
        new Color(0.95f, 0.55f, 0.65f, 1f)  // 연홍색 (부드러운 영기)
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

    public void SpawnDeathSouls(Vector2 centerPosition, int count = 5)
    {
        SpawnDeathSouls(centerPosition, count, 1f, 1f);
    }

    /// <summary>
    /// 사망 소울 (원형으로 퍼짐). totalAmount = 전체 소울 충전량 (1 = 슬롯 1칸),
    /// speedMultiplier = 퍼지는 속도 배율 (보스처럼 많이 뿌릴 때 크게)
    /// </summary>
    public void SpawnDeathSouls(Vector2 centerPosition, int count, float totalAmount, float speedMultiplier)
    {
        SpawnDeathSoulRing(centerPosition, count, totalAmount / Mathf.Max(1, count), speedMultiplier, 0f, 0f);
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
        float step = 360f / perBurst;

        for (int burst = 0; burst < bursts; burst++)
        {
            // 매번 각도를 조금씩 돌려서 같은 방향으로 겹치지 않게
            float angleOffset = (step * 0.5f * burst) + Random.Range(0f, step * 0.3f);
            // 회차마다 터지는 중심을 반경 안에서 무작위로
            Vector2 burstCenter = centerPosition + (Random.insideUnitCircle * spawnRadius);
            SpawnDeathSoulRing(burstCenter, perBurst, amountPerSoul, speedMultiplier, angleOffset, spawnRadius * 0.3f);

            if (burst < bursts - 1 && interval > 0f)
            {
                yield return new WaitForSeconds(interval);
            }
        }
    }

    /// <param name="jitterRadius">소울마다 생성 위치를 이 반경 안에서 살짝 흩뜨림</param>
    private void SpawnDeathSoulRing(
        Vector2 centerPosition,
        int count,
        float amountPerSoul,
        float speedMultiplier,
        float angleOffset,
        float jitterRadius
    )
    {
        count = Mathf.Max(1, count);

        for (int i = 0; i < count; i++)
        {
            Color color = SoulColors[i % SoulColors.Length];
            float angle = angleOffset + ((360f / count) * i) + Random.Range(-20f, 20f);
            float rad = angle * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 velocity = direction * (Random.Range(3.5f, 5.5f) * speedMultiplier);

            float waitTime = Random.Range(0.12f, 0.22f);
            Vector2 spawnPosition = centerPosition + (Random.insideUnitCircle * jitterRadius);
            SpawnCustomSoul(spawnPosition, amountPerSoul, color, 1.15f, velocity, waitTime);
        }
    }

    public Color GetRandomSoulColor()
    {
        return SoulColors[Random.Range(0, SoulColors.Length)];
    }
}
