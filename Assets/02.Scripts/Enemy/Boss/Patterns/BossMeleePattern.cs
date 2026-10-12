using System.Collections;
using UnityEngine;

/// <summary>
/// 앞발로 때리기 (1층 근접 공격). 플레이어가 가까이 있을 때 앞발을 휘둘러 앞쪽을 침.
/// 애니메이션 시작 ~ 판정 시작: 예고(선택) / 판정 시작 ~ 끝: 앞발 판정.
/// Animation Trigger에 "melee", Animation Clip에 앞발 클립을 넣고, Hit Start / End Ratio로 휘두르는 순간을 맞출 것
/// </summary>
public class BossMeleePattern : BossPattern
{
    [Header("앞발")]
    [Tooltip("앞발 판정 (보스 자식, 보스 앞쪽 바닥 높이에 배치). BossHitbox + Collider2D(Is Trigger)")]
    [SerializeField]
    private BossHitbox _hitbox;

    [Tooltip("판정 시작 전까지 예고 표시(BossHitbox의 Telegraph)를 보여줌")]
    [SerializeField]
    private bool _showTelegraph = false;

    [Header("사용 거리 (보스와 플레이어의 가로 거리)")]
    [Tooltip("플레이어가 이 거리보다 가까우면 사용 안 함 (0이면 제한 없음)")]
    [SerializeField, Min(0f)]
    private float _minDistance = 0f;

    [Tooltip("플레이어가 이 거리 안에 있을 때만 사용 (앞발이 닿는 거리 정도)")]
    [SerializeField, Min(0f)]
    private float _maxDistance = 5f;

    [Header("내딛기 (선택)")]
    [Tooltip("휘두르는 동안 앞으로 내딛는 거리. 0이면 제자리")]
    [SerializeField, Min(0f)]
    private float _stepDistance = 0f;

    [Tooltip("맵 양 끝에서 이만큼 안쪽까지만 내딛음")]
    [SerializeField, Min(0f)]
    private float _edgeMargin = 2f;

    [Header("타격감")]
    [Tooltip("판정이 켜지는 순간 카메라 흔들림")]
    [SerializeField]
    private CameraShakeSettings _swingShake = new CameraShakeSettings(0.12f, 0.15f);

    private void Reset()
    {
        SetDefaults("앞발 때리기", BossPhaseMask.Both, BossFloorMask.Floor1);
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
        if (_hitbox == null)
        {
            Debug.LogWarning($"{name}: {PatternName} 앞발 판정(Hitbox)이 비어 있어 애니메이션만 재생합니다.", this);
            yield break;
        }

        float direction = Boss.FacingX;
        float startX = Boss.Position.x;
        float targetX = startX + (direction * _stepDistance);
        if (Boss.Arena != null)
        {
            targetX = Boss.Arena.ClampX(targetX, _edgeMargin);
        }

        if (_showTelegraph)
        {
            _hitbox.SetTelegraph(true);
        }

        // 판정 시작까지 대기 (패링 그로기 판정이면 직전에 반짝임). 내딛기가 있으면 그동안 앞으로 이동
        bool hasWarned = false;
        while (Progress < HitStartRatio)
        {
            hasWarned = UpdateParryWarning(_hitbox, HitStartRatio, hasWarned);
            MoveStep(startX, targetX);
            yield return null;
        }

        _hitbox.SetTelegraph(false);
        MoveStep(startX, targetX);

        // 휘두르기
        _hitbox.Activate();
        CameraShake.Shake(_swingShake);
        AudioAction("Boss.Melee");

        yield return WaitUntilProgress(HitEndRatio);
        _hitbox.Deactivate();
    }

    /// <summary>판정 시작 지점까지 진행도에 맞춰 앞으로 내딛음</summary>
    private void MoveStep(float startX, float targetX)
    {
        if (_stepDistance <= 0f)
        {
            return;
        }

        float t = HitStartRatio > 0f ? Mathf.Clamp01(Progress / HitStartRatio) : 1f;
        float x = Mathf.Lerp(startX, targetX, Mathf.SmoothStep(0f, 1f, t));
        Boss.MoveTo(new Vector2(x, Boss.Position.y));
    }

    protected override void Cleanup()
    {
        if (_hitbox != null)
        {
            _hitbox.Deactivate();
            _hitbox.SetTelegraph(false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;
        Gizmos.color = new Color(1f, 0.4f, 0.4f, 0.8f);
        Gizmos.DrawLine(center + (Vector3.left * _maxDistance), center + (Vector3.right * _maxDistance));

        if (_minDistance > 0f)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.8f);
            Gizmos.DrawLine(center + (Vector3.left * _minDistance) + (Vector3.up * 0.2f), center + (Vector3.right * _minDistance) + (Vector3.up * 0.2f));
        }
    }
}
