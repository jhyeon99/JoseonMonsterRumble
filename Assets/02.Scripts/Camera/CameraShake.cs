using UnityEngine;
using System.Collections;

public class CameraShake : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float _duration = 0.1f;

    [SerializeField] private float _damageMultiplier = 0.02f;
    [SerializeField] private float _maxMagnitude = 0.3f;

    private Vector3 _shakeOffset = Vector3.zero;

    private void OnEnable() => GameEvents.OnPlayerHit += PlayerHitCameraShake;
    private void OnDisable() => GameEvents.OnPlayerHit -= PlayerHitCameraShake;

    private Coroutine _cameraShakeRoutine;

    private void LateUpdate()
    {
        if (_shakeOffset != Vector3.zero)
        {
            transform.position += _shakeOffset;
        }
    }

    private void PlayerHitCameraShake(HitData hitData)
    {
        float magnitude = Mathf.Min(hitData.Damage * _damageMultiplier, _maxMagnitude);
        if (_cameraShakeRoutine != null)
        {
            StopCoroutine(_cameraShakeRoutine);
        }

        _cameraShakeRoutine = StartCoroutine(DoShake(_duration, magnitude));
    }

    private IEnumerator DoShake(float duration, float magnitude)
    {
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            Vector2 randomCircle = Random.insideUnitCircle * magnitude;
            // 멀미 방지를 위해 Y축은 30%만 반영
            _shakeOffset = new Vector3(randomCircle.x, randomCircle.y * 0.3f, 0f);

            yield return null;
        }

        _shakeOffset = Vector3.zero;
    }
}