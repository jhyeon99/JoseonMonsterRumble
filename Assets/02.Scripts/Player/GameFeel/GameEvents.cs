using System;
using UnityEngine;

public class GameEvents
{
    public static event Action<HitData> OnPlayerHit;

    public static void TriggerPlayerHit(HitData hitData)
    {
        OnPlayerHit?.Invoke(hitData);
    }

    public static event Action<HitData> OnEnemyHit;

    public static void TriggerEnemyHit(HitData hitData)
    {
        OnEnemyHit?.Invoke(hitData);
    }

    /// <summary>플레이어가 패링에 성공했을 때 (패링당한 공격 정보)</summary>
    public static event Action<HitData> OnParry;

    public static void TriggerParry(HitData hitData)
    {
        OnParry?.Invoke(hitData);
    }

    /// <summary>마무리 공격 타격 순간 (타격 위치)</summary>
    public static event Action<Vector2> OnFinisher;

    /// <summary>마무리 공격 화면 연출에 필요한 공격자와 대상.</summary>
    public static event Action<Transform, Transform> OnFinisherImpact;

    /// <summary>마무리 공격 타격 전의 애니메이션 진행도. 0은 공격 시작, 1은 타격 프레임.</summary>
    public static event Action<Transform, Transform, float> OnFinisherAnticipation;

    public static event Action OnFinisherAnticipationCancelled;

    public static void TriggerFinisherAnticipation(Transform attacker, Transform target, float progress)
    {
        OnFinisherAnticipation?.Invoke(attacker, target, progress);
    }

    public static void TriggerFinisherAnticipationCancelled()
    {
        OnFinisherAnticipationCancelled?.Invoke();
    }

    public static void TriggerFinisher(Vector2 point, Transform attacker, Transform target)
    {
        OnFinisherImpact?.Invoke(attacker, target);
        TriggerFinisher(point);
    }

    public static void TriggerFinisher(Vector2 point)
    {
        OnFinisher?.Invoke(point);
    }
}
