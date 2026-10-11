using System.Collections;
using UnityEngine;

/// <summary>
/// 짧은 점프 접근. 플레이어가 가까이 있을 때 플레이어 쪽으로 짧게 뛰어서 덮침.
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

    [Tooltip("한 번에 이동하는 최대 가로 거리. 플레이어가 더 가까우면 플레이어 위치까지만")]
    [SerializeField, Min(0f)]
    private float _hopDistance = 3f;

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

    [Tooltip("플레이어 위치보다 이만큼 앞에 착지 (0이면 플레이어 위치까지 덮침)")]
    [SerializeField, Min(0f)]
    private float _stopShortDistance = 0f;

    [Tooltip("맵 양 끝에서 이만큼 안쪽까지만 이동")]
    [SerializeField, Min(0f)]
    private float _edgeMargin = 2f;

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
        yield return WaitUntilProgress(_takeoffRatio);

        Boss.FacePlayer();
        float startX = Boss.Position.x;
        float groundY = Boss.GroundY;
        float targetX = GetTargetX(startX);
        float landRatio = Mathf.Max(_landRatio, _takeoffRatio + 0.01f);

        while (Progress < landRatio)
        {
            float t = Mathf.InverseLerp(_takeoffRatio, landRatio, Progress);
            float x = Mathf.Lerp(startX, targetX, t);
            float y = groundY + (4f * _hopHeight * t * (1f - t));
            Boss.MoveTo(new Vector2(x, y));
            yield return new WaitForFixedUpdate();
        }

        Boss.MoveTo(new Vector2(targetX, groundY));
        Boss.SnapToGround();
        CameraShake.Shake(_landingShake);
    }

    /// <summary>플레이어 쪽으로 최대 Hop Distance만큼, 플레이어보다 Stop Short Distance만큼 앞까지</summary>
    private float GetTargetX(float startX)
    {
        if (Boss.Player == null)
        {
            return startX;
        }

        float toPlayer = Boss.Player.position.x - startX;
        float distance = Mathf.Clamp(Mathf.Abs(toPlayer) - _stopShortDistance, 0f, _hopDistance);
        float x = startX + (Mathf.Sign(toPlayer) * distance);
        return Boss.Arena != null ? Boss.Arena.ClampX(x, _edgeMargin) : x;
    }

    protected override void Cleanup()
    {
        // 공중에서 끊겼으면 땅으로
        if (Boss != null)
        {
            Boss.SnapToGround();
        }
    }

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
