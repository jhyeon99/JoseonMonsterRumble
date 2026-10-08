using UnityEngine;

/// <summary>
/// 플레이어 마무리 공격(PlayerFinisher)의 대상이 될 수 있는 대상 (그로기 일반 적, 최종 그로기 보스 등).
/// </summary>
public interface IFinishable
{
    /// <summary>지금 마무리 공격을 받을 수 있는지 (그로기 중 등)</summary>
    bool CanBeFinished { get; }

    /// <summary>위치 / 방향 계산용</summary>
    Transform FinisherTransform { get; }

    /// <summary>마무리 공격 준비 동작 중 그로기가 풀리지 않게 고정. 고정할 수 없으면 false</summary>
    bool TryHoldForFinisher();

    /// <summary>마무리 공격이 타격 전에 취소됐을 때 고정 해제</summary>
    void ReleaseFinisherHold();

    /// <summary>마무리 공격 타격 순간</summary>
    /// <param name="launch">플레이어 쪽 날려 보내기 설정 (일반 적용. 보스는 무시 가능)</param>
    /// <param name="directionX">플레이어 → 대상 방향 (1 또는 -1)</param>
    void ReceiveFinisher(LaunchSettings launch, float directionX);
}
