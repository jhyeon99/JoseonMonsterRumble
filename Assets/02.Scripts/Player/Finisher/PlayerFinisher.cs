using UnityEngine;

/// <summary>
/// 그로기 적 마무리 공격 설정.
/// 그로기 적이 앞쪽 탐지 범위 안에 있을 때 공격키 → 일반 공격 대신 3타 모션을 바로 재생하고 (PlayerFinisherState),
/// 타격 순간에 적을 회전시키며 작아지게 하늘로 날려 보냄.
/// </summary>
public class PlayerFinisher : MonoBehaviour
{
    [Header("대상 탐지")]
    [Tooltip("탐지 범위 중심. 플레이어 위치 기준, x는 바라보는 방향 쪽")]
    [SerializeField]
    private Vector2 _detectOffset = new Vector2(0.9f, 0.6f);

    [SerializeField] private Vector2 _detectSize = new Vector2(1.8f, 1.6f);

    [SerializeField] private LayerMask _enemyLayer = Constants.Layers.EnemyMask;

    [Header("타이밍")]
    [Tooltip("켜면 3타 AttackData의 판정 시작 비율(HitStartRatio)과 3타 애니메이션 길이로 타격 시점을 계산함. 끄거나 계산할 수 없으면 아래 수동 값을 사용")]
    [SerializeField]
    private bool _useAttackHitTiming = true;

    [Tooltip("수동: 마무리 공격 시작부터 적이 날아갈 때까지 (초)")]
    [SerializeField, Min(0f)]
    private float _impactDelay = 0.55f;

    [Tooltip("수동: 마무리 공격 상태 전체 길이 (초). 끝나면 이동 / 대기로 돌아감")]
    [SerializeField, Min(0.1f)]
    private float _duration = 1.2f;

    [Header("날려 보내기 연출")]
    [SerializeField]
    private LaunchSettings _launch = LaunchSettings.Default;

    private static readonly Collider2D[] Hits = new Collider2D[16];

    // 마무리는 3타 모션 사용 (Player.PlayFinisherAnimation과 같은 기준)
    private const int ComboIndex = 2;
    private const string ReadyClipName = "Player_Attack_3rd_Ready";
    private const string AttackClipName = "Player_Attack_3rd";
    private const string RecoverClipName = "Player_Attack_3rd_Recover";

    private bool _hasCachedTiming;
    private float _cachedImpactDelay;
    private float _cachedDuration;

    public LaunchSettings Launch => _launch;

    /// <summary>
    /// 타격 시점과 전체 길이 (초).
    /// 일반 공격과 같은 방식으로 (준비 + 공격 클립 길이) x HitStartRatio 지점에서 타격.
    /// 전체 길이는 준비 + 공격 + 회복 클립 길이.
    /// </summary>
    public void GetTiming(Animator animator, PlayerAttack attack, out float impactDelay, out float duration)
    {
        if (!_hasCachedTiming)
        {
            CacheTiming(animator, attack);
        }

        impactDelay = _cachedImpactDelay;
        duration = _cachedDuration;
    }

    private void CacheTiming(Animator animator, PlayerAttack attack)
    {
        _cachedImpactDelay = _impactDelay;
        _cachedDuration = Mathf.Max(_duration, _impactDelay);

        // 애니메이터가 아직 준비되지 않았으면 캐시하지 않고 다음에 다시 계산
        if (!_useAttackHitTiming || animator == null || animator.runtimeAnimatorController == null)
        {
            _hasCachedTiming = !_useAttackHitTiming;
            return;
        }

        _hasCachedTiming = true;

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        float ready = GetClipLength(clips, ReadyClipName);
        float attackLength = GetClipLength(clips, AttackClipName);
        float recover = GetClipLength(clips, RecoverClipName);
        float stepDuration = ready + attackLength;

        if (stepDuration <= 0.0001f)
        {
            Debug.LogWarning($"{name}: 마무리용 공격 클립을 찾지 못해 수동 타이밍을 사용합니다.", this);
            return;
        }

        if (attack == null || !attack.TryGetAttackData(ComboIndex, out AttackData data) ||
            data.HitEndRatio <= data.HitStartRatio)
        {
            Debug.LogWarning($"{name}: 3타 AttackData의 판정 비율이 비어 있어 수동 타이밍을 사용합니다.", this);
            return;
        }

        _cachedImpactDelay = stepDuration * data.HitStartRatio;
        _cachedDuration = recover > 0f
            ? stepDuration + recover
            : Mathf.Max(stepDuration, _duration);
    }

    private static float GetClipLength(AnimationClip[] clips, string clipName)
    {
        if (clips == null || string.IsNullOrEmpty(clipName))
        {
            return 0f;
        }

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null && clips[i].name == clipName)
            {
                return clips[i].length;
            }
        }

        return 0f;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 인스펙터에서 값을 바꾸면 다시 계산
        _hasCachedTiming = false;
    }
#endif

    /// <summary>앞쪽 탐지 범위 안에서 가장 가까운 그로기 적</summary>
    public bool TryFindTarget(out Enemy target)
    {
        target = null;

        var filter = new ContactFilter2D();
        filter.SetLayerMask(_enemyLayer);
        filter.useTriggers = true;

        int count = Physics2D.OverlapBox(GetDetectCenter(), _detectSize, 0f, filter, Hits);
        float closest = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Enemy enemy = Hits[i].GetComponentInParent<Enemy>();
            if (enemy == null || !enemy.IsGroggy)
            {
                continue;
            }

            float distance = Mathf.Abs(enemy.transform.position.x - transform.position.x);
            if (distance < closest)
            {
                closest = distance;
                target = enemy;
            }
        }

        return target != null;
    }

    private Vector2 GetDetectCenter()
    {
        float facing = transform.localScale.x >= 0f ? 1f : -1f;
        return (Vector2)transform.position + new Vector2(_detectOffset.x * facing, _detectOffset.y);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.8f);
        Gizmos.DrawWireCube(GetDetectCenter(), _detectSize);
    }
}
