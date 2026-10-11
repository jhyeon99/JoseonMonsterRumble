using UnityEngine;

/// <summary>
/// 보스 침뱉기 투사체. 중력을 받아 포물선으로 날아감 (발사 속도는 BossSpitPattern이 Launch로 지정).
/// 플레이어 / 바닥 / 벽에 닿으면 사라짐 (Projectile 기본 충돌 처리). 패링하면 보스 쪽으로 반사.
/// 프리팹의 Type을 BossSpit으로 두고 ProjectilePool의 투사체 프리팹 목록에 등록해서 사용.
/// </summary>
public class BossSpitProjectile : Projectile
{
    [Header("포물선")]
    [Tooltip("아래로 당기는 가속도 (클수록 빨리 떨어지고 궤적이 둥글게 휨)")]
    [SerializeField, Min(0.01f)]
    private float _gravity = 8f;

    [Tooltip("날아가는 방향으로 스프라이트를 돌림")]
    [SerializeField]
    private bool _rotateToVelocity = true;

    [Tooltip("스프라이트 원본의 앞쪽이 왼쪽이면 체크 (적 투사체 기본). 오른쪽이면 끄기")]
    [SerializeField]
    private bool _spriteFacesLeft = true;

    private Vector2 _velocity;

    /// <summary>패턴이 발사 속도를 계산할 때 쓰는 중력</summary>
    public float Gravity => _gravity;

    protected override Vector2 HitDirection =>
        IsReflected ? ReflectDirection : (_velocity.sqrMagnitude > 0.0001f ? _velocity.normalized : Vector2.down);

    public override void OnSpawn(Transform muzzlePoint, Transform playerPoint, float damageIncreaseValue)
    {
        base.OnSpawn(muzzlePoint, playerPoint, damageIncreaseValue);
        transform.position = muzzlePoint.position;

        // 패턴이 Launch를 따로 부르지 않으면 플레이어 위치로 1초 동안 날아가는 포물선
        Vector2 target = playerPoint != null ? (Vector2)playerPoint.position : MuzzlePosition + (Vector2.left * 5f);
        Launch(CalculateVelocity(MuzzlePosition, target, 1f, _gravity));
    }

    /// <summary>발사 속도 지정 (위치는 그대로)</summary>
    public void Launch(Vector2 velocity)
    {
        _velocity = velocity;
        _isLaunching = true;
        UpdateRotation();
    }

    /// <summary>from에서 출발해 flightTime초 뒤 to를 지나는 발사 속도</summary>
    public static Vector2 CalculateVelocity(Vector2 from, Vector2 to, float flightTime, float gravity)
    {
        float time = Mathf.Max(0.05f, flightTime);
        Vector2 offset = to - from;
        return new Vector2(offset.x / time, (offset.y / time) + (0.5f * gravity * time));
    }

    protected override void Fire()
    {
        if (!_isLaunching)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        Vector2 acceleration = Vector2.down * _gravity;
        transform.position += (Vector3)((_velocity * deltaTime) + (acceleration * (0.5f * deltaTime * deltaTime)));
        _velocity += acceleration * deltaTime;
        UpdateRotation();
    }

    private void UpdateRotation()
    {
        if (!_rotateToVelocity || _velocity.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float angle = Mathf.Atan2(_velocity.y, _velocity.x) * Mathf.Rad2Deg;
        if (_spriteFacesLeft)
        {
            angle += 180f;
        }

        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void OnDisable()
    {
        _velocity = Vector2.zero;
        _isLaunching = false;
    }
}
