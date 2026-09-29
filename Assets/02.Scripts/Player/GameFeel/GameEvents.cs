using System;

public class GameEvents
{
    public static event Action<HitData> OnPlayerHit;

    public static void TriggerPlayerHit(HitData hitData)
    {
        OnPlayerHit?.Invoke(hitData);
    }
}