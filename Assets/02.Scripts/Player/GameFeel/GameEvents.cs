using System;
using UnityEngine;

public class GameEvents
{
    public static event Action<HitData> OnPlayerHit;

    public static void TriggerPlayerHit(HitData hitData)
    {
        OnPlayerHit?.Invoke(hitData);
    }

    /// <summary>플레이어가 패링에 성공했을 때 (패링당한 공격 정보)</summary>
    public static event Action<HitData> OnParry;

    public static void TriggerParry(HitData hitData)
    {
        OnParry?.Invoke(hitData);
    }

    /// <summary>마무리 공격 타격 순간 (타격 위치)</summary>
    public static event Action<Vector2> OnFinisher;

    public static void TriggerFinisher(Vector2 point)
    {
        OnFinisher?.Invoke(point);
    }
}