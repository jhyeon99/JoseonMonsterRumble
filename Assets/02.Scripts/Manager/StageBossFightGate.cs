using System.Collections;
using UnityEngine;

/// <summary>씬 페이드 / 리스폰 로딩 화면이 끝날 때까지 보스 전투 시작을 기다린다.</summary>
[DisallowMultipleComponent]
public sealed class StageBossFightGate : MonoBehaviour
{
    private Boss _boss;
    private Coroutine _startRoutine;

    public static bool TryDefer(Boss boss)
    {
        if (!IsWaiting)
        {
            return false;
        }

        StageBossFightGate gate = boss.GetComponent<StageBossFightGate>();
        if (gate == null)
        {
            gate = boss.gameObject.AddComponent<StageBossFightGate>();
        }

        gate._boss = boss;
        if (gate._startRoutine == null)
        {
            gate._startRoutine = gate.StartCoroutine(gate.StartFightAfterFade());
        }
        return true;
    }

    private IEnumerator StartFightAfterFade()
    {
        while (IsWaiting)
        {
            if (_boss == null || !_boss.isActiveAndEnabled)
            {
                _startRoutine = null;
                yield break;
            }
            yield return null;
        }

        _startRoutine = null;
        if (_boss != null && _boss.isActiveAndEnabled)
        {
            _boss.StartFight();
        }
    }

    // 리스폰 중(로딩 화면이 걷히기 전)에도 대기. 등장 연출을 생략하면 보스가 로딩 화면 뒤에서 먼저 움직이는 것 방지
    private static bool IsWaiting =>
        SceneArrivalFade.IsTransitioning
        || (StageManager.Instance != null && StageManager.Instance.IsRespawning);

    public static void Cancel(Boss boss)
    {
        StageBossFightGate gate = boss.GetComponent<StageBossFightGate>();
        if (gate != null)
        {
            gate.CancelPendingStart();
        }
    }

    private void OnDisable()
    {
        CancelPendingStart();
    }

    private void CancelPendingStart()
    {
        if (_startRoutine != null)
        {
            StopCoroutine(_startRoutine);
            _startRoutine = null;
        }
    }
}
