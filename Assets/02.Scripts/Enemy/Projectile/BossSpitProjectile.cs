/// <summary>
/// 보스 침뱉기 투사체. 동작은 직선 투사체(FlyingProjectile)와 같고, 풀에서 따로 꺼내 쓰도록 타입만 분리 (ProjectileType.BossSpit).
/// 프리팹의 Type을 BossSpit으로 두고 ProjectilePool의 투사체 프리팹 목록에 등록해서 사용.
/// </summary>
public class BossSpitProjectile : FlyingProjectile
{
}
