using System.Collections;
using UnityEngine;

/// <summary>씬 페이드가 끝날 때까지 보스 전투 시작을 기다린다.</summary>
[DisallowMultipleComponent]
public sealed class StageBossFightGate : MonoBehaviour
{
    private Boss _boss;
    private Coroutine _startRoutine;

    public static bool TryDefer(Boss boss)
    {
        if (!SceneArrivalFade.IsTransitioning)
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
        while (SceneArrivalFade.IsTransitioning)
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
