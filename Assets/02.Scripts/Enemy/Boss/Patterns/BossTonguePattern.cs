using System.Collections;
using UnityEngine;

/// <summary>
/// 혓바닥 공격. 2층용 / 3층용으로 컴포넌트를 두 개 붙이고 각각 판정과 층을 지정.
/// 예고 → 혀 뻗기(판정) → 회수
/// </summary>
public class BossTonguePattern : BossPattern
{
    [Header("혓바닥")]
    [Tooltip("혀 판정 (보스 자식, 해당 층 높이에 배치)")]
    [SerializeField]
    private BossHitbox _hitbox;

    [SerializeField, Min(0f)] private float _telegraphTime = 0.7f;
    [SerializeField, Min(0f)] private float _activeTime = 0.3f;
    [SerializeField, Min(0f)] private float _recoverTime = 0.6f;

    [Header("애니메이션 (없으면 무시)")]
    [SerializeField] private string _readyTrigger = "tongueReady";
    [SerializeField] private string _attackTrigger = "tongueAttack";

    private void Reset()
    {
        SetDefaults("혓바닥 (2층)", BossPhaseMask.Both, BossFloorMask.Floor2);
    }

    protected override IEnumerator Execute()
    {
        Boss.FacePlayer();
        Boss.SetTrigger(_readyTrigger);
        _hitbox.SetTelegraph(true);

        yield return new WaitForSeconds(_telegraphTime);

        _hitbox.SetTelegraph(false);
        Boss.SetTrigger(_attackTrigger);
        _hitbox.Activate();

        yield return new WaitForSeconds(_activeTime);

        _hitbox.Deactivate();

        yield return new WaitForSeconds(_recoverTime);
    }

    protected override void Cleanup()
    {
        if (_hitbox != null)
        {
            _hitbox.Deactivate();
            _hitbox.SetTelegraph(false);
        }
    }

    protected override void OnInitialize()
    {
        if (_hitbox == null)
        {
            Debug.LogWarning($"{name}: {PatternName}에 판정(BossHitbox)이 연결되지 않아 사용하지 않습니다.", this);
            enabled = false;
        }
    }
}
