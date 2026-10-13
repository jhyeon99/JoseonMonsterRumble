using System.Collections;
using UnityEngine;

/// <summary>
/// 짧은 점프. 플레이어가 가까이 있을 때 플레이어를 뛰어넘어 살짝 반대편에 착지.
/// - 별도 공격 판정 없음. 보스 몸통 접촉 데미지(EnemyContactDamage)로 공격 (Disable Contact Damage는 꺼 둘 것)
/// - 0 ~ 이륙 비율: 제자리 (웅크리기), 이륙 ~ 착지 비율: 점프 이동, 이후: 착지 후 마무리
/// - 점프 횟수가 2 이상이면 애니메이션을 처음부터 다시 재생해 연속으로 뜀
/// (판정 시작 / 끝 비율은 사용하지 않음)
/// </summary>
public class BossHopPattern : BossPattern
{
    [Header("사용 거리 (보스와 플레이어의 가로 거리)")]
    [Tooltip("플레이어가 이 거리보다 가까우면 사용 안 함 (0이면 제한 없음)")]
    [SerializeField, Min(0f)]
    private float _minDistance = 0f;

    [Tooltip("플레이어가 이 거리 안에 있을 때만 사용")]
    [SerializeField, Min(0f)]
    private float _maxDistance = 6f;

    [Header("점프 (비율은 애니메이션 길이 대비)")]
    [Tooltip("연속으로 뛰는 횟수")]
    [SerializeField, Min(1)]
    private int _hopCount = 1;

    [Tooltip("한 번에 이동하는 최대 가로 거리. 플레이어 + 넘어가는 거리가 이보다 멀면 여기까지만 (넘어가지 못할 수 있음)")]
    [SerializeField, Min(0f)]
    private float _hopDistance = 8f;

    [Tooltip("점프 높이")]
    [SerializeField, Min(0f)]
    private float _hopHeight = 1.5f;

    [Tooltip("땅에서 떨어지는 지점")]
    [SerializeField, Range(0f, 1f)]
    private float _takeoffRatio = 0.2f;

    [Tooltip("착지하는 지점")]
    [SerializeField, Range(0f, 1f)]
    private float _landRatio = 0.7f;

    [Tooltip("마지막이 아닌 점프에서 다음 점프를 시작하는 지점. 1 = 애니메이션이 끝난 뒤")]
    [SerializeField, Range(0f, 1f)]
    private float _nextHopRatio = 1f;

    [Tooltip("플레이어를 넘어 반대편으로 이만큼 더 가서 착지 (0이면 플레이어 위치에 착지)")]
    [SerializeField, Min(0f)]
    private float _overshootDistance = 1.5f;

    [Tooltip("넘어가는 거리에 더해지는 랜덤 폭 (± 값)")]
    [SerializeField, Min(0f)]
    private float _overshootJitter = 0.3f;

    [Tooltip("맵 양 끝에서 이만큼 안쪽까지만 이동")]
    [SerializeField, Min(0f)]
    private float _edgeMargin = 2f;

    [Tooltip("뛰어오를 때 발밑 먼지 (크기 / 위치는 Boss의 점프 먼지 설정)")]
    [SerializeField]
    private bool _takeoffDust = true;

    [Tooltip("착지할 때 발밑 먼지")]
    [SerializeField]
    private bool _landingDust = true;

    [SerializeField]
    private CameraShakeSettings _landingShake = new CameraShakeSettings(0.12f, 0.12f);

    private void Reset()
    {
        SetDefaults("짧은 점프 접근", BossPhaseMask.Both, BossFloorMask.Floor1);
    }

    /// <summary>기본 조건(페이즈 / 층 / 쿨타임) + 플레이어가 사용 거리 안에 있을 때</summary>
    public override bool CanUse()
    {
        if (!base.CanUse() || Boss.Player == null)
        {
            return false;
        }

        float distance = Mathf.Abs(Boss.Player.position.x - Boss.Position.x);
        return distance >= _minDistance && distance <= _maxDistance;
    }

    protected override IEnumerator Execute()
    {
        float nextHopRatio = Mathf.Max(_nextHopRatio, _landRatio);

        for (int i = 0; i < _hopCount; i++)
        {
            if (i > 0)
            {
                // 같은 애니메이션을 처음부터 다시 재생 (진행도도 0부터)
                Boss.FacePlayer();
                ReplayAnimation();
            }

            yield return Hop();

            if (i < _hopCount - 1)
            {
                yield return WaitUntilProgress(nextHopRatio);
            }
        }
    }

    /// <summary>이륙 비율까지 대기 → 플레이어 쪽으로 포물선 이동 → 착지</summary>
    private IEnumerator Hop()
    {
        // 이륙 먼지를 일찍 내는 경우 (Boss의 Takeoff Dust Timing이 음수)
        float takeoffTiming = Boss.TakeoffDustTiming;

        // 높은 곳(시작 위치 등)에서 출발하면 발밑에 땅이 없으므로 이륙 먼지 생략
        bool startsOnGround = Mathf.Abs(Boss.Position.y - Boss.GroundY) < 0.1f;
        bool takeoffDust = _takeoffDust && startsOnGround;

        if (takeoffDust && takeoffTiming < 0f)
        {
            float earlyRatio = _takeoffRatio + (takeoffTiming / Mathf.Max(0.0001f, Duration));
            yield return WaitUntilProgress(Mathf.Max(0f, earlyRatio));
            Boss.PlayJumpDust(Boss.Position.x, Boss.GroundY);
        }

        yield return WaitUntilProgress(_takeoffRatio);

        Boss.FacePlayer();
        float startX = Boss.Position.x;
        // 지금 높이에서 출발 (높은 시작 위치에서도 바닥으로 먼저 내려가지 않고 그 자리에서 뜀)
        float startY = Boss.Position.y;
        float groundY = Boss.GroundY;
        float targetX = GetTargetX(startX);
        float landRatio = Mathf.Max(_landRatio, _takeoffRatio + 0.01f);

        if (takeoffDust && takeoffTiming >= 0f)
        {
            Boss.PlayJumpDust(startX, groundY, takeoffTiming);
        }

        float landingTiming = Boss.LandingDustTiming;
        bool landingDustPlayed = !_landingDust;

        _airborne = true;
        while (Progress < landRatio)
        {
            float t = Mathf.InverseLerp(_takeoffRatio, landRatio, Progress);
            float x = Mathf.Lerp(startX, targetX, t);
            // 출발 높이에서 먼저 위로 뛰어오른 뒤 바닥으로 떨어지는 포물선 (같은 높이면 기존과 동일)
            float y = GetHopY(t, startY, groundY);
            Boss.MoveTo(new Vector2(x, y));

            // 착지 직전에 미리 (Landing Dust Timing이 음수)
            if (!landingDustPlayed && landingTiming < 0f && (landRatio - Progress) * Duration <= -landingTiming)
            {
                Boss.PlayJumpDust(targetX, groundY);
                landingDustPlayed = true;
            }

            yield return new WaitForFixedUpdate();
        }

        Boss.MoveTo(new Vector2(targetX, groundY));
        Boss.SnapToGround();
        _airborne = false;
        CameraShake.Shake(_landingShake);

        if (!landingDustPlayed)
        {
            Boss.PlayJumpDust(targetX, groundY, landingTiming);
        }
    }

    /// <summary>
    /// 출발 높이 → 최고점(높은 쪽 + Hop Height) → 착지 높이. 중력처럼 올라갈 때 / 내려올 때 시간을 높이에 맞춰 나눔.
    /// 높은 곳에서 출발해도 바로 떨어지지 않고 그 자리에서 위로 뛰어오른 뒤 내려옴
    /// </summary>
    private float GetHopY(float t, float startY, float landY)
    {
        float apexY = Mathf.Max(startY, landY) + _hopHeight;
        float rise = Mathf.Max(0f, apexY - startY);
        float fall = Mathf.Max(0f, apexY - landY);
        if (rise + fall <= 0.0001f)
        {
            return Mathf.Lerp(startY, landY, t);
        }

        float apexT = Mathf.Sqrt(rise) / (Mathf.Sqrt(rise) + Mathf.Sqrt(fall));
        if (t < apexT)
        {
            float u = (apexT - t) / Mathf.Max(0.0001f, apexT);
            return apexY - (rise * u * u);
        }

        float v = (t - apexT) / Mathf.Max(0.0001f, 1f - apexT);
        return apexY - (fall * v * v);
    }

    /// <summary>플레이어를 넘어 Overshoot Distance만큼 반대편까지 (최대 Hop Distance, 맵 안쪽으로 제한)</summary>
    private float GetTargetX(float startX)
    {
        if (Boss.Player == null)
        {
            return startX;
        }

        float toPlayer = Boss.Player.position.x - startX;

        // 플레이어가 바로 위 / 아래면 보스가 보는 방향으로
        float direction = Mathf.Abs(toPlayer) > 0.01f ? Mathf.Sign(toPlayer) : Boss.FacingX;

        float overshoot = Mathf.Max(0f, _overshootDistance + Random.Range(-_overshootJitter, _overshootJitter));
        float distance = Mathf.Min(Mathf.Abs(toPlayer) + overshoot, _hopDistance);
        float x = startX + (direction * distance);
        return Boss.Arena != null ? Boss.Arena.ClampX(x, _edgeMargin) : x;
    }

    protected override void Cleanup()
    {
        // 점프 도중(공중)에 끊겼을 때만 땅으로.
        // (패턴 시작 시에도 정리가 한 번 불리므로, 항상 붙이면 높은 시작 위치에서 바닥으로 순간이동함)
        if (_airborne && Boss != null)
        {
            Boss.SnapToGround();
        }

        _airborne = false;
    }

    private bool _airborne;

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
        Gizmos.DrawLine(center + (Vector3.left * _maxDistance), center + (Vector3.right * _maxDistance));

        if (_minDistance > 0f)
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
            Gizmos.DrawLine(center + (Vector3.left * _minDistance) + (Vector3.up * 0.2f), center + (Vector3.right * _minDistance) + (Vector3.up * 0.2f));
        }
    }
}
