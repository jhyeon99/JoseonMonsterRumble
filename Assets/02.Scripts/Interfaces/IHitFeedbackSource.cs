using System;

/// <summary>
/// 맞았을 때 피격 연출(EnemyHitFeedback)에 알려 주는 대상.
/// EnemyHealth / BossHealth가 아닌 오브젝트(올챙이, 개구리알 등)가 구현하면 EnemyHitFeedback을 붙여 쓸 수 있음
/// </summary>
public interface IHitFeedbackSource
{
    /// <summary>공격을 받아 데미지가 들어감 (피격 연출용)</summary>
    event Action<HitData> OnHitFeedback;
}
