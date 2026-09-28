using UnityEngine;
using System.Collections;

public class HitStop : MonoBehaviour
{
    [SerializeField] private float _baseDuration = 0.1f;

    private void OnEnable() => GameEvents.OnPlayerHit += PlayerHitStop;
    private void OnDisable() => GameEvents.OnPlayerHit -= PlayerHitStop;

    private void PlayerHitStop(HitData hitData)
    {
        float duration = _baseDuration + (hitData.Damage * 0.005f);
        StartCoroutine(DoHitStop(duration));
    }

    private IEnumerator DoHitStop(float duration)
    {
        Time.timeScale = 0;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
    }
}