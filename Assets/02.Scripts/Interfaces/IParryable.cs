using UnityEngine;

/// <summary>
/// 패링당했을 때 반응이 있는 공격이 구현 (그로기, 투사체 반사 등).
/// 패링 가능 여부는 HitData.Defense.Parryable로 정하고,
/// 반응이 필요하면 HitData.Source에 자신을 넣는다. (Source 없이도 패링 자체는 가능)
/// </summary>
public interface IParryable
{
    /// <param name="hitData">패링당한 공격 정보</param>
    /// <param name="parrier">패링한 대상 (플레이어)</param>
    void OnParried(HitData hitData, Transform parrier);
}
