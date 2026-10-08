using System;

/// <summary>패턴을 쓸 수 있는 페이즈 (여러 개 선택 가능)</summary>
[Flags]
public enum BossPhaseMask
{
    None = 0,
    Phase1 = 1 << 0,
    Phase2 = 1 << 1,
    Both = Phase1 | Phase2,
}

/// <summary>플레이어가 있는 층 (여러 개 선택 가능)</summary>
[Flags]
public enum BossFloorMask
{
    None = 0,
    Floor1 = 1 << 0,
    Floor2 = 1 << 1,
    Floor3 = 1 << 2,
    Any = Floor1 | Floor2 | Floor3,
}
