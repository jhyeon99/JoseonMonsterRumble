using System;

namespace GameAudio
{
    /// <summary>
    /// 예약부터 재생 종료까지 동일한 요청을 식별한다. AudioSource를 노출하지 않는다.
    /// 세대는 요청 슬롯 재사용 시 갱신하며, 유효성은 재생 서비스의 기록과 대조해야 한다.
    /// </summary>
    public readonly struct PlayHandle : IEquatable<PlayHandle>
    {
        public static PlayHandle Empty => default;
        public ulong RequestId { get; }
        public uint Generation { get; }
        public bool IsEmpty => RequestId == 0 || Generation == 0;

        public PlayHandle(ulong requestId, uint generation)
        {
            RequestId = requestId;
            Generation = generation;
        }

        public bool Equals(PlayHandle other) => RequestId == other.RequestId && Generation == other.Generation;
        public override bool Equals(object obj) => obj is PlayHandle other && Equals(other);
        public override int GetHashCode() => unchecked(RequestId.GetHashCode() * 397 ^ Generation.GetHashCode());
        public static bool operator ==(PlayHandle left, PlayHandle right) => left.Equals(right);
        public static bool operator !=(PlayHandle left, PlayHandle right) => !left.Equals(right);
    }
}
