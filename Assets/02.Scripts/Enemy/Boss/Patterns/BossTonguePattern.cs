using System.Collections;
using UnityEngine;

/// <summary>
/// 혓바닥 공격. 2층용 / 3층용으로 컴포넌트를 두 개 붙이고 각각 판정과 층을 지정.
/// 애니메이션 시작 ~ 판정 시작: 예고 / 판정 시작 ~ 끝: 혀 판정
/// </summary>
public class BossTonguePattern : BossPattern
{
    [Header("혓바닥")]
    [Tooltip("혀 판정 (보스 자식, 해당 층 높이에 배치)")]
    [SerializeField]
    private BossHitbox _hitbox;

    private void Reset()
    {
        SetDefaults("혓바닥 (2층)", BossPhaseMask.Both, BossFloorMask.Floor2);
    }

    protected override void OnInitialize()
    {
        if (_hitbox == null)
        {
            Debug.LogWarning($"{name}: {PatternName}에 판정(BossHitbox)이 연결되지 않아 사용하지 않습니다.", this);
            enabled = false;
        }
    }

    protected override IEnumerator Execute()
    {
        yield return RunHitWindow(_hitbox);
    }

    protected override void Cleanup()
    {
        if (_hitbox != null)
        {
            _hitbox.Deactivate();
            _hitbox.SetTelegraph(false);
        }
    }
}
