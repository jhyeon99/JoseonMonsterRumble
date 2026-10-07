using UnityEngine;
using System.Collections;

[DefaultExecutionOrder(10)]
public class CameraShake : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float _duration = 0.1f;

    [SerializeField] private float _damageMultiplier = 0.02f;
    [SerializeField] private float _maxMagnitude = 0.3f;

    [Header("패링")]
    [SerializeField] private float _parryDuration = 0.12f;
    [SerializeField] private float _parryMagnitude = 0.15f;

    [Header("마무리 공격")]
    [SerializeField] private float _finisherDuration = 0.25f;
    [SerializeField] private float _finisherMagnitude = 0.35f;

    private Vector3 _shakeOffset = Vector3.zero;

    private void OnEnable()
    {
        GameEvents.OnPlayerHit += PlayerHitCameraShake;
        GameEvents.OnParry += ParryCameraShake;
        GameEvents.OnFinisher += FinisherCameraShake;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerHit -= PlayerHitCameraShake;
        GameEvents.OnParry -= ParryCameraShake;
        GameEvents.OnFinisher -= FinisherCameraShake;
        ResetShake();
    }

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
        StartShake(_duration, magnitude);
    }

    private void ParryCameraShake(HitData hitData)
    {
        StartShake(_parryDuration, _parryMagnitude);
    }

    private void FinisherCameraShake(Vector2 point)
    {
        StartShake(_finisherDuration, _finisherMagnitude);
    }

    private void StartShake(float duration, float magnitude)
    {
        if (_cameraShakeRoutine != null)
        {
            StopCoroutine(_cameraShakeRoutine);
        }

        _cameraShakeRoutine = StartCoroutine(DoShake(duration, magnitude));
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

    private void ResetShake()
    {
        if (_cameraShakeRoutine != null)
        {
            _shakeOffset = Vector3.zero;
            _cameraShakeRoutine = null;
        }
    }
}