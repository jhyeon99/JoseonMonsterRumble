using System.Collections;
using UnityEngine;

/// <summary>
/// 짧은 점프. 플레이어가 가까이 있을 때 플레이어를 뛰어넘어 살짝 반대편에 착지.
/// - 별도 공격 판정 없음. 보스 몸통 접촉 데미지(EnemyContactDamage)로 공격 (Disable Contact Damage는 꺼 둘 것)
/// - 애니메이션: 시작(Boss_Move_Start) → 공중(Boss_Move_Air, 체공 시간 동안 유지) → 착지(Boss_Move_End). 상태 이름은 Boss에서 설정
/// - 시작 애니메이션이 끝나는 순간 이륙, Air Speed로 일정하게 가로 이동 (체공 시간 = 거리 / 속도) 후 착지
/// - 점프 횟수가 2 이상이면 착지 후 다시 시작부터 연속으로 뜀
/// (Animation Trigger / Animation Clip, 판정 시작 / 끝 비율은 사용하지 않음)
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

    [Header("점프")]
    [Tooltip("연속으로 뛰는 횟수")]
    [SerializeField, Min(1)]
    private int _hopCount = 1;

    [Tooltip("한 번에 이동하는 최대 가로 거리. 플레이어 + 넘어가는 거리가 이보다 멀면 여기까지만 (넘어가지 못할 수 있음)")]
    [SerializeField, Min(0f)]
    private float _hopDistance = 8f;

    [Tooltip("점프 높이")]
    [SerializeField, Min(0f)]
    private float _hopHeight = 1.5f;

    [Tooltip("공중에서 가로로 이동하는 속도 (초당 거리). 멀리 뛸수록 오래 떠 있음 (체공 시간 = 거리 / 속도)")]
    [SerializeField, Min(0.1f)]
    private float _airSpeed = 24f;

    [Tooltip("체공 시간 최소 (초). 아주 가까이 뛸 때도 이만큼은 떠 있음")]
    [SerializeField, Min(0.05f)]
    private float _minAirDuration = 0.15f;

    [Tooltip("체공 시간 최대 (초)")]
    [SerializeField, Min(0.05f)]
    private float _maxAirDuration = 0.75f;

    [Tooltip("연속 점프일 때 착지 후 다음 점프까지 (초). 0 = 착지 애니메이션이 끝난 뒤, 양수 = 착지하고 그만큼 뒤 바로")]
    [SerializeField, Min(0f)]
    private float _nextHopDelay = 0f;

    [Tooltip("플레이어를 넘어 반대편으로 이만큼 더 가서 착지 (0이면 플레이어 위치에 착지)")]
    [SerializeField, Min(0f)]
    private float _overshootDistance = 1.5f;

    [Tooltip("넘어가는 거리에 더해지는 랜덤 폭 (± 값)")]
    [SerializeField, Min(0f)]
    private float _overshootJitter = 0.3f;

    [Tooltip("맵 양 끝에서 이만큼 안쪽까지만 이동")]
    [SerializeField, Min(0f)]
    private float _edgeMargin = 2f;

    [Header("후퇴")]
    [Tooltip("플레이어를 바라본 채 반대 방향으로 점프. 벽 때문에 이동할 공간이 없으면 플레이어를 넘어감")]
    [SerializeField] private bool _retreatFromPlayer;
    [SerializeField, Min(0f)] private float _minimumRetreatTravel = 4f;

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
        if (distance < _minDistance || distance > _maxDistance) return false;
        if (!_retreatFromPlayer) return true;
        float startX = Boss.Position.x;
        float targetX = GetTargetX(startX);
        float toPlayer = Boss.Player.position.x - startX;
        float direction = Mathf.Abs(toPlayer) > 0.01f ? Mathf.Sign(toPlayer) : Boss.FacingX;
        bool movesAway = (targetX - startX) * direction < 0f;
        bool crossesPlayer = (targetX - Boss.Player.position.x) * direction > 0f;
        return Mathf.Abs(targetX - startX) >= _minimumRetreatTravel && (movesAway || crossesPlayer);
    }

    /// <summary>패턴 트리거 / 클립 대신 Boss의 점프 상태(시작 → 공중 → 착지)를 직접 재생</summary>
    protected override bool UsesPatternAnimation => false;

    /// <summary>애니메이션 길이가 아니라 체공 시간으로 끝을 정함</summary>
    protected override bool WaitForDurationAfterExecute => false;

    protected override IEnumerator Execute()
    {
        for (int i = 0; i < _hopCount; i++)
        {
            yield return Hop();

            bool isLast = i == _hopCount - 1;
            if (!isLast && _nextHopDelay > 0f)
            {
                yield return new WaitForSeconds(_nextHopDelay);
                continue;
            }

            // 착지 애니메이션이 끝날 때까지 (끝나면 애니메이터 전이로 대기)
            yield return WaitJumpEnd();
        }
    }

    /// <summary>시작 애니메이션 → (끝나는 순간) 이륙 → 공중에서 포물선 이동 → 착지 애니메이션</summary>
    private IEnumerator Hop()
    {
        // 이륙 먼지를 일찍 내는 경우 (Boss의 Takeoff Dust Timing이 음수)
        float takeoffTiming = Boss.TakeoffDustTiming;

        // 높은 곳(시작 위치 등)에서 출발하면 발밑에 땅이 없으므로 이륙 먼지 생략
        bool startsOnGround = Mathf.Abs(Boss.Position.y - Boss.GroundY) < 0.1f;
        bool takeoffDust = _takeoffDust && startsOnGround;
        bool takeoffDustPlayed = !takeoffDust;

        Boss.FacePlayer();
        Boss.PlayJumpStart();

        // 애니메이터가 시작 상태로 바뀌는 건 다음 갱신부터
        yield return null;

        // 이륙 지점(Boss의 Jump Takeoff Ratio)까지 제자리 (웅크리기)
        float timeout = Time.time + Boss.JumpStateTimeout;
        while (Boss.IsBeforeJumpTakeoff && Time.time < timeout)
        {
            if (!takeoffDustPlayed && takeoffTiming < 0f && Boss.JumpTakeoffRemaining <= -takeoffTiming)
            {
                Boss.PlayJumpDust(Boss.Position.x, Boss.GroundY);
                takeoffDustPlayed = true;
            }

            yield return null;
        }

        // 이륙
        Boss.FacePlayer();
        Boss.PlayJumpAir();

        float startX = Boss.Position.x;
        // 지금 높이에서 출발 (높은 시작 위치에서도 바닥으로 먼저 내려가지 않고 그 자리에서 뜀)
        float startY = Boss.Position.y;
        float groundY = Boss.GroundY;
        float targetX = GetTargetX(startX);

        if (!takeoffDustPlayed)
        {
            Boss.PlayJumpDust(startX, groundY, Mathf.Max(0f, takeoffTiming));
        }

        float landingTiming = Boss.LandingDustTiming;
        bool landingDustPlayed = !_landingDust;

        // 거리에 따라 체공 시간이 정해짐 (같은 속도로 이동)
        float airDuration = GetAirDuration(Mathf.Abs(targetX - startX));

        _airborne = true;
        bool endAnimationPlayed = false;
        bool footstepPlayed = false;
        float elapsed = 0f;
        while (elapsed < airDuration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / airDuration);
            float x = Mathf.Lerp(startX, targetX, t);
            // 출발 높이에서 먼저 위로 뛰어오른 뒤 바닥으로 떨어지는 포물선 (같은 높이면 기존과 동일)
            float y = GetHopY(t, startY, groundY);
            Boss.MoveTo(new Vector2(x, y));

            // 공중 애니메이션 유지, 착지 직전(Boss의 Jump End Lead Time)부터 착지 애니메이션
            endAnimationPlayed = Boss.UpdateJumpAirAnimation(airDuration - elapsed, endAnimationPlayed);
            footstepPlayed = PlayLandingFootstep(airDuration - elapsed, footstepPlayed);

            // 착지 직전에 미리 (Landing Dust Timing이 음수)
            if (!landingDustPlayed && landingTiming < 0f && airDuration - elapsed <= -landingTiming)
            {
                Boss.PlayJumpDust(targetX, groundY);
                landingDustPlayed = true;
            }

            yield return new WaitForFixedUpdate();
        }

        // 착지
        Boss.MoveTo(new Vector2(targetX, groundY));
        Boss.SnapToGround();
        _airborne = false;
        if (!endAnimationPlayed)
        {
            Boss.PlayJumpEnd();
        }

        PlayLandingFootstep(0f, footstepPlayed);
        CameraShake.Shake(_landingShake);

        if (!landingDustPlayed)
        {
            Boss.PlayJumpDust(targetX, groundY, landingTiming);
        }
    }

    /// <summary>체공 시간 = 가로 거리 / 속도 (최소 / 최대 사이)</summary>
    private float GetAirDuration(float distance)
    {
        float min = Mathf.Min(_minAirDuration, _maxAirDuration);
        float max = Mathf.Max(_minAirDuration, _maxAirDuration);
        return Mathf.Clamp(distance / Mathf.Max(0.1f, _airSpeed), min, max);
    }

    /// <summary>착지 애니메이션이 끝날 때까지 대기 (최대 Boss의 Jump State Timeout)</summary>
    private IEnumerator WaitJumpEnd()
    {
        // 애니메이터가 착지 상태로 바뀌는 건 다음 갱신부터
        yield return null;

        float timeout = Time.time + Boss.JumpStateTimeout;
        while (Boss.IsInJumpEnd && Time.time < timeout)
        {
            yield return null;
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

        if (_retreatFromPlayer)
        {
            float retreatX = startX - direction * _hopDistance;
            if (Boss.Arena != null) retreatX = Boss.Arena.ClampX(retreatX, _edgeMargin);
            if (Mathf.Abs(retreatX - startX) >= _minimumRetreatTravel) return retreatX;
        }

        float overshoot = Mathf.Max(0f, _overshootDistance + (_retreatFromPlayer ? 0f : Random.Range(-_overshootJitter, _overshootJitter)));
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
