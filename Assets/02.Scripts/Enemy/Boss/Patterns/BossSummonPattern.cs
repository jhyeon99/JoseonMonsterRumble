using System.Collections;
using UnityEngine;

/// <summary>
/// 소환 (2페이즈). 애니메이션의 소환 비율 지점에서 독 방울 소환 → 잠시 뒤 아기가 나옴.
/// 아기는 공격 패턴 없이 접촉 데미지만 있고, 플레이어 콤보 3타에 맞으면 보스에게 날아가 기믹 데미지.
/// (판정 시작 / 끝 비율은 사용하지 않음)
/// </summary>
public class BossSummonPattern : BossPattern
{
    [Header("소환")]
    [SerializeField] private BossPoisonBubble _bubblePrefab;

    [Tooltip("독 방울이 생길 수 있는 위치들 (무작위로 선택)")]
    [SerializeField]
    private Transform[] _spawnPoints;

    [Tooltip("소환 지점 (애니메이션 길이 대비 비율)")]
    [SerializeField, Range(0f, 1f)]
    private float _spawnRatio = 0.6f;

    [Tooltip("한 번에 소환하는 독 방울 수")]
    [SerializeField, Min(1)]
    private int _bubbleCount = 2;

    [Tooltip("맵에 남아 있는 소환물(방울 + 아기)이 이 수 이상이면 사용하지 않음")]
    [SerializeField, Min(1)]
    private int _maxMinions = 4;

    private void Reset()
    {
        SetDefaults("소환", BossPhaseMask.Phase2, BossFloorMask.Any);
    }

    protected override void OnInitialize()
    {
        if (_bubblePrefab == null || _spawnPoints == null || _spawnPoints.Length == 0)
        {
            Debug.LogWarning($"{name}: {PatternName}에 독 방울 프리팹 또는 소환 위치가 없어 사용하지 않습니다.", this);
            enabled = false;
        }
    }

    public override bool CanUse()
    {
        return base.CanUse() && Boss.MinionCount < _maxMinions;
    }

    protected override IEnumerator Execute()
    {
        yield return WaitUntilProgress(_spawnRatio);

        int count = Mathf.Min(_bubbleCount, _spawnPoints.Length);
        int start = Random.Range(0, _spawnPoints.Length);

        for (int i = 0; i < count; i++)
        {
            // 시작 위치를 무작위로 정하고 겹치지 않게 순서대로 사용
            Transform point = _spawnPoints[(start + i) % _spawnPoints.Length];
            if (point == null)
            {
                continue;
            }

            BossPoisonBubble bubble = Instantiate(_bubblePrefab, point.position, Quaternion.identity);
            bubble.Initialize(Boss);
        }
    }
}
