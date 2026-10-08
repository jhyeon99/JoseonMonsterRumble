using System.Collections;
using UnityEngine;

/// <summary>
/// 돌진 (2페이즈). 애니메이션 시작 ~ 판정 시작: 예고 / 판정 시작 ~ 끝: 바라보는 방향 맵 끝까지 돌진(판정).
/// 돌진 속도는 판정 구간 안에 맵 끝에 도착하도록 자동 계산. 판정 높이를 2층 점프로 피할 수 있게 배치.
/// </summary>
public class BossChargePattern : BossPattern
{
    [Header("돌진")]
    [Tooltip("돌진 판정 (보스 몸 앞쪽)")]
    [SerializeField]
    private BossHitbox _hitbox;

    [Tooltip("맵 끝에서 이만큼 앞에서 멈춤")]
    [SerializeField, Min(0f)]
    private float _edgeMargin = 2f;

    [Tooltip("맵 정보가 없을 때 돌진 거리")]
    [SerializeField, Min(0f)]
    private float _fallbackDistance = 12f;

    [SerializeField] private CameraShakeSettings _stopShake = new CameraShakeSettings(0.3f, 0.2f);

    private float _direction;

    private void Reset()
    {
        SetDefaults("돌진", BossPhaseMask.Phase2, BossFloorMask.Floor1 | BossFloorMask.Floor2);
    }

    protected override void OnInitialize()
    {
        if (_hitbox == null)
        {
            Debug.LogWarning($"{name}: {PatternName}에 판정(BossHitbox)이 연결되지 않아 사용하지 않습니다.", this);
            enabled = false;
        }
    }

    protected override void OnPatternStart()
    {
        base.OnPatternStart();
        _direction = Boss.FacingX;
    }

    protected override IEnumerator Execute()
    {
        _hitbox.SetTelegraph(true);
        yield return WaitUntilProgress(HitStartRatio);

        _hitbox.SetTelegraph(false);
        _hitbox.Activate();

        float startX = Boss.Position.x;
        float targetX = GetTargetX(startX, _direction);
        float span = Mathf.Max(0.0001f, HitEndRatio - HitStartRatio);

        while (Progress < HitEndRatio)
        {
            float t = Mathf.Clamp01((Progress - HitStartRatio) / span);
            Boss.MoveTo(new Vector2(Mathf.Lerp(startX, targetX, t), Boss.GroundY));
            yield return new WaitForFixedUpdate();
        }

        Boss.MoveTo(new Vector2(targetX, Boss.GroundY));
        _hitbox.Deactivate();
        CameraShake.Shake(_stopShake);
    }

    protected override void Cleanup()
    {
        if (_hitbox != null)
        {
            _hitbox.Deactivate();
            _hitbox.SetTelegraph(false);
        }
    }

    private float GetTargetX(float startX, float direction)
    {
        if (Boss.Arena == null)
        {
            return startX + (direction * _fallbackDistance);
        }

        float edge = direction > 0f ? Boss.Arena.RightX - _edgeMargin : Boss.Arena.LeftX + _edgeMargin;

        // 이미 끝에 붙어 있으면 제자리
        return direction > 0f ? Mathf.Max(edge, startX) : Mathf.Min(edge, startX);
    }
}
