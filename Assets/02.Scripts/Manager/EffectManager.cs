using UnityEngine;

/// <summary>
/// 이펙트 재생 창구 (싱글톤). 상황별 메서드를 제공하고, 실제 생성과 풀링은 EffectPool에 맡김.
/// 사용: EffectManager.Instance.PlayPlayerHit(hitData);
/// 새 이펙트가 생기면 EffectType에 추가하고, 여기에 상황별 메서드를 하나 추가.
/// </summary>
[RequireComponent(typeof(EffectPool))]
public class EffectManager : MonoBehaviour
{
    [Header("적 피격 이펙트 크기")]
    [SerializeField, Min(0f)] private float _lightEnemyHitScale = 0.8f;
    [SerializeField, Min(0f)] private float _heavyEnemyHitScale = 1.3f;
    [SerializeField, Min(0f)] private float _talismanHitScale = 0.65f;

    [Header("보스 입 이펙트 (침뱉기 / 하울링)")]
    [Tooltip("보스 입 이펙트 원본 이미지가 오른쪽을 보고 있으면 체크. 보스가 원본과 반대 방향을 보면 좌우 반전 (EffectPool에서 Allow Flip을 켜야 적용)")]
    [SerializeField] private bool _bossMouthVfxFacesRight = false;
    [SerializeField, Min(0f)] private float _bossSpitScale = 1f;
    [SerializeField, Min(0f)] private float _bossHowlScale = 1f;

    private static EffectManager _instance = null;
    public static EffectManager Instance
    {
        get
        {
            if (_instance == null)
            {
                EffectManager existing = FindFirstObjectByType<EffectManager>();
                if (existing != null)
                {
                    _instance = existing;
                }
                else
                {
                    EffectManager prefab = Resources.Load<EffectManager>("PF_EffectManager");
                    if (prefab != null)
                    {
                        _instance = Instantiate(prefab);
                        _instance.name = "EffectManager";
                    }
                }
            }
            return _instance;
        }
    }

    private EffectPool _pool;

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
        _pool = GetComponent<EffectPool>();

        if (_pool == null)
        {
            Debug.LogWarning("[EffectManager] 같은 오브젝트에 EffectPool이 없습니다.", this);
        }
    }

    private void OnEnable()
    {
        // Awake에서 중복으로 판정된 오브젝트는 Destroy가 프레임 끝에 일어나서 OnEnable이 불림
        if (_instance != this) return;

        // HitStop, CameraShake와 같은 방식으로 플레이어 피격 이벤트 구독
        GameEvents.OnPlayerHit += PlayPlayerHit;
        GameEvents.OnEnemyHit += PlayEnemyHit;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerHit -= PlayPlayerHit;
        GameEvents.OnEnemyHit -= PlayEnemyHit;
    }

    /// <summary>플레이어 피격 위치에 피격 이펙트 재생</summary>
    public void PlayPlayerHit(HitData hitData)
    {
        // AttackDirection.x < 0 → 왼쪽으로 밀리는 공격 = 오른쪽에서 맞음 → 좌우 반전
        Play(EffectType.PlayerHit, hitData.HitPoint, hitData.AttackDirection.x < 0f);
    }

    public void PlayEnemyHit(HitData hitData)
    {
        float scale = hitData.IsTalisman
            ? _talismanHitScale
            : hitData.IsHeavyImpact ? _heavyEnemyHitScale : _lightEnemyHitScale;
        Play(EffectType.EnemyHit, hitData.HitPoint, hitData.AttackDirection.x < 0f, scale);
    }

    /// <summary>보스 침뱉기 순간 입에서 터지는 이펙트</summary>
    public void PlayBossSpit(Vector2 mouthPosition, bool bossFacingRight)
    {
        Play(EffectType.BossSpit, mouthPosition, bossFacingRight != _bossMouthVfxFacesRight, _bossSpitScale);
    }

    /// <summary>보스 하울링(페이즈 전환) 때 입에서 터지는 이펙트</summary>
    public void PlayBossHowl(Vector2 mouthPosition, bool bossFacingRight)
    {
        Play(EffectType.BossHowl, mouthPosition, bossFacingRight != _bossMouthVfxFacesRight, _bossHowlScale);
    }

    /// <summary>
    /// 상황별 메서드가 아직 없는 이펙트용. 가능하면 상황별 메서드를 추가해서 사용.
    /// </summary>
    public void Play(EffectType type, Vector2 position, bool flipX = false, float scale = 1f)
    {
        if (_pool == null) return;

        _pool.Play(type, position, flipX, scale);
    }
}
