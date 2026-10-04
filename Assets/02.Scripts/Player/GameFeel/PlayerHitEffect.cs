using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 피격 시 HitData.HitPoint 위치에 파티클 이펙트를 생성 (풀링).
/// 씬의 아무 오브젝트에 붙이고 _effectPrefab에 파티클 프리팹을 넣으면 동작.
/// </summary>
public class PlayerHitEffect : MonoBehaviour
{
    [Header("이펙트")]
    [SerializeField] private ParticleSystem _effectPrefab;

    [Tooltip("HitPoint 기준 위치 보정")]
    [SerializeField] private Vector2 _offset = Vector2.zero;

    [Tooltip("공격 방향에 맞춰 이펙트를 좌우 반전 (Y축 180도 회전)")]
    [SerializeField] private bool _flipByAttackDirection = true;

    [Header("풀링")]
    [SerializeField, Min(1)] private int _prewarmCount = 5;

    private readonly Queue<ParticleSystem> _pool = new Queue<ParticleSystem>();

    private void Awake()
    {
        if (_effectPrefab == null)
        {
            Debug.LogWarning("[PlayerHitEffect] _effectPrefab이 비어 있습니다.");
            return;
        }

        for (int i = 0; i < _prewarmCount; i++)
        {
            _pool.Enqueue(CreateInstance());
        }
    }

    private void OnEnable() => GameEvents.OnPlayerHit += SpawnEffect;

    private void OnDisable() => GameEvents.OnPlayerHit -= SpawnEffect;

    private void SpawnEffect(HitData hitData)
    {
        if (_effectPrefab == null) return;

        ParticleSystem effect = _pool.Count > 0 ? _pool.Dequeue() : CreateInstance();

        Transform effectTransform = effect.transform;
        effectTransform.position = hitData.HitPoint + _offset;

        Quaternion baseRotation = _effectPrefab.transform.rotation;
        bool attackFromRight = hitData.AttackDirection.x < 0f; // 왼쪽으로 밀리는 공격 = 오른쪽에서 맞음
        effectTransform.rotation = _flipByAttackDirection && attackFromRight
            ? Quaternion.Euler(0f, 180f, 0f) * baseRotation
            : baseRotation;

        effect.gameObject.SetActive(true);
        effect.Play(true);

        StartCoroutine(ReturnWhenFinished(effect));
    }

    private IEnumerator ReturnWhenFinished(ParticleSystem effect)
    {
        // Play 직후 첫 프레임에는 IsAlive가 false일 수 있어서 한 프레임 대기
        yield return null;

        while (effect != null && effect.IsAlive(true))
        {
            yield return null;
        }

        if (effect == null) yield break;

        effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        effect.gameObject.SetActive(false);
        _pool.Enqueue(effect);
    }

    private ParticleSystem CreateInstance()
    {
        ParticleSystem effect = Instantiate(_effectPrefab, transform);

        // 풀링과 맞지 않는 프리팹 설정을 런타임에 보정 (자식 파티클 포함)
        ParticleSystem[] systems = effect.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem system in systems)
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.loop = false;                                 // 반복이면 끝나지 않아 풀로 못 돌아옴
            main.playOnAwake = false;                          // 재생은 SpawnEffect에서 직접
            main.stopAction = ParticleSystemStopAction.None;   // Destroy/Disable이면 풀이 깨짐
        }

        effect.gameObject.SetActive(false);
        return effect;
    }
}
