using System;

public interface ISoulSource
{
    event Action<float, float> OnSoulChanged;

    float CurrentSoul { get; }
    float MaxSoul { get; }
}
