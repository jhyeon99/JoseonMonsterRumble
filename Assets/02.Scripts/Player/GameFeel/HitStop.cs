using UnityEngine;
using System.Collections;

public class HitStop : MonoBehaviour
{
    [SerializeField] private float _baseDuration = 0.05f;
    [SerializeField] private float _hitStopTimeScale = 0.02f;

    private void OnEnable() => GameEvents.OnPlayerHit += PlayerHitStop;

    private void OnDisable()
    {
        GameEvents.OnPlayerHit -= PlayerHitStop;
        RestoreTimeScale();
    }

    private Coroutine _hitStopRoutine;

    private void PlayerHitStop(HitData hitData)
    {
        float duration = _baseDuration + (hitData.Damage * 0.005f);
        if (_hitStopRoutine != null)
        {
            StopCoroutine(_hitStopRoutine);
        }

        _hitStopRoutine = StartCoroutine(DoHitStop(duration));
    }

    private IEnumerator DoHitStop(float duration)
    {
        Time.timeScale = 0;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
    }

    private void RestoreTimeScale()
    {
        if (_hitStopRoutine != null)
        {
            Time.timeScale = 1f;
            _hitStopRoutine = null;
        }
    }
}