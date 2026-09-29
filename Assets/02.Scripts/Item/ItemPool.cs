using UnityEngine;

public class ItemPool : MonoBehaviour
{
    private static ItemPool _instance = null;
    public static ItemPool Instance => _instance;

    [Header("아이템 프리팹")]
    [SerializeField] private PotionItem _potionPrefab;

    [SerializeField] private SoulItem _soulPrefab;

    [Header("풀 사이즈")]
    [SerializeField] private int _potionPoolSize = 10;

    [SerializeField] private int _soulPoolSize = 30;

    private PotionItem[] _potionPool;
    private SoulItem[] _soulPool;

    private void Awake()
    {
        if (_instance != null)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        InitPool();
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

        _instance = this;
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
}