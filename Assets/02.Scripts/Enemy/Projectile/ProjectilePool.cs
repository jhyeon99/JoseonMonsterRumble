using UnityEngine;

public class ProjectilePool : MonoBehaviour
{
    private static ProjectilePool _instance = null;
    public static ProjectilePool Instance => _instance;

    [Header("투사체 프리팹")]

    [SerializeField] private Projectile[] _projectilePrefabs;

    [Header("풀 사이즈")]

    [SerializeField] private int _poolSize = 20;

    private Projectile[,] _pool;

    private void Awake()
    {
        if (_instance != null)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        _pool = new Projectile[_projectilePrefabs.Length, _poolSize];

        for (int i = 0; i < _projectilePrefabs.Length; i++)
        {
            Projectile projectilePrefab = _projectilePrefabs[i];
            for (int j = 0; j < _poolSize; j++)
            {
                Projectile projectile = Instantiate(projectilePrefab, gameObject.transform);
                projectile.gameObject.SetActive(false);
                _pool[i, j] = projectile;
            }
        }
    }

    public Projectile GetProjectile(ProjectileType projectiletype, Transform muzzlePoint, Transform playerPoint,
        float damageIncreaseValue)
    {
        for (int i = 0; i < _pool.GetLength(0); i++)
        {
            if (_pool[i, 0].Type != projectiletype)
            {
                continue;
            }

            for (int j = 0; j < _pool.GetLength(1); j++)
            {
                Projectile projectile = _pool[i, j];

                if (projectile.gameObject.activeSelf == false)
                {
                    projectile.gameObject.SetActive(true);
                    projectile.TrailPrefab.SetActive(false);
                    projectile.OnSpawn(muzzlePoint, playerPoint, damageIncreaseValue);
                    return projectile;
                }
            }
        }

        return null;
    }
}
