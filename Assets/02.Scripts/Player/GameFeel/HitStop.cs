using UnityEngine;
using System.Collections;

public class HitStop : MonoBehaviour
{
    [SerializeField] private float _baseDuration = 0.05f;
    [SerializeField] private float _hitStopTimeScale = 0.02f;

    [Header("패링")]
    [SerializeField] private float _parryDuration = 0.12f;

    private void OnEnable()
    {
        GameEvents.OnPlayerHit += PlayerHitStop;
        GameEvents.OnParry += ParryHitStop;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerHit -= PlayerHitStop;
        GameEvents.OnParry -= ParryHitStop;
        RestoreTimeScale();
    }

    private Coroutine _hitStopRoutine;

    private void PlayerHitStop(HitData hitData)
    {
        StartHitStop(_baseDuration + (hitData.Damage * 0.005f));
    }

    private void ParryHitStop(HitData hitData)
    {
        StartHitStop(_parryDuration);
    }

    private void StartHitStop(float duration)
    {
        // 일시정지 중에는 timeScale을 건드리지 않음 (일시정지가 우선)
        if (PauseController.IsPaused) return;

        if (_hitStopRoutine != null)
        {
            StopCoroutine(_hitStopRoutine);
        }

        _hitStopRoutine = StartCoroutine(DoHitStop(duration));
    }

    private IEnumerator DoHitStop(float duration)
    {
        Time.timeScale = _hitStopTimeScale;
        yield return new WaitForSecondsRealtime(duration);

        // 히트스톱 도중 일시정지했으면 그대로 둠 (재개 시 PauseController가 1로 되돌림)
        if (!PauseController.IsPaused)
        {
            Time.timeScale = 1f;
        }

        _hitStopRoutine = null;
    }

    private void RestoreTimeScale()
    {
        if (_hitStopRoutine != null)
        {
            if (!PauseController.IsPaused)
            {
                Time.timeScale = 1f;
            }

            _hitStopRoutine = null;
        }
    }
}