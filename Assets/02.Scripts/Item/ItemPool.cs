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

        return null;
    }

    public void SpawnHitSoul(Vector2 hitPosition, bool isHeavy)
    {
        float amount = isHeavy ? 0.35f : 0.15f;
        float scale = isHeavy ? 1.35f : 1.0f;
        Color color = GetRandomSoulColor();

        Vector2 randomDir = (Vector2.up * 1.5f + Random.insideUnitCircle * 1.2f).normalized;
        Vector2 velocity = randomDir * Random.Range(2.5f, 4.5f);

        SpawnCustomSoul(hitPosition, amount, color, scale, velocity, Random.Range(0.08f, 0.15f));
    }

    public void SpawnDeathSouls(Vector2 centerPosition, int count = 5)
    {
        float amountPerSoul = 1.0f / Mathf.Max(1, count); // 총 1.0 (슬롯 1칸 풀충전)

        for (int i = 0; i < count; i++)
        {
            Color color = SoulColors[i % SoulColors.Length];
            float angle = (360f / count) * i + Random.Range(-20f, 20f);
            float rad = angle * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 velocity = direction * Random.Range(3.5f, 5.5f);

            float waitTime = Random.Range(0.12f, 0.22f);
            SpawnCustomSoul(centerPosition, amountPerSoul, color, 1.15f, velocity, waitTime);
        }
    }

    public Color GetRandomSoulColor()
    {
        return SoulColors[Random.Range(0, SoulColors.Length)];
    }
}
