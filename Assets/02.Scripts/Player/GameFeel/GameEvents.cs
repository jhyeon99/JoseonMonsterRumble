using System;

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
}