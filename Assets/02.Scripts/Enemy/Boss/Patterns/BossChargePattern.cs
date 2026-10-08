using System.Collections;
using UnityEngine;

/// <summary>
/// 돌진 (2페이즈). 예고 → 바라보는 방향의 맵 끝까지 돌진(판정) → 정지.
/// 판정 높이를 2층 점프로 피할 수 있게 배치.
/// </summary>
public class BossChargePattern : BossPattern
{
    [Header("돌진")]
    [Tooltip("돌진 판정 (보스 몸 앞쪽)")]
    [SerializeField]
    private BossHitbox _hitbox;

    [SerializeField, Min(0f)] private float _windupTime = 0.8f;
    [SerializeField, Min(0.1f)] private float _chargeSpeed = 14f;

    [Tooltip("맵 끝에서 이만큼 앞에서 멈춤")]
    [SerializeField, Min(0f)]
    private float _edgeMargin = 2f;

    [Tooltip("맵 정보가 없을 때 돌진 거리")]
    [SerializeField, Min(0f)]
    private float _fallbackDistance = 12f;

    [SerializeField, Min(0f)] private float _recoverTime = 1f;
    [SerializeField] private CameraShakeSettings _stopShake = new CameraShakeSettings(0.3f, 0.2f);

    [Header("애니메이션 (없으면 무시)")]
    [SerializeField] private string _readyTrigger = "chargeReady";
    [SerializeField] private string _chargeTrigger = "charge";
    [SerializeField] private string _stopTrigger = "chargeStop";

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

    protected override IEnumerator Execute()
    {
        Boss.FacePlayer();
        float direction = Boss.FacingX;

        Boss.SetTrigger(_readyTrigger);
        _hitbox.SetTelegraph(true);

        yield return new WaitForSeconds(_windupTime);

        _hitbox.SetTelegraph(false);
        Boss.SetTrigger(_chargeTrigger);
        _hitbox.Activate();

        float startX = Boss.Position.x;
        float targetX = GetTargetX(startX, direction);

        // 막혀서 못 가는 경우를 대비해 예상 시간 + 1초가 지나면 멈춤
        float maxTime = (Mathf.Abs(targetX - startX) / _chargeSpeed) + 1f;
        float elapsed = 0f;

        while (!Mathf.Approximately(Boss.Position.x, targetX) && elapsed < maxTime)
        {
            elapsed += Time.fixedDeltaTime;
            float x = Mathf.MoveTowards(Boss.Position.x, targetX, _chargeSpeed * Time.fixedDeltaTime);
            Boss.MoveTo(new Vector2(x, Boss.GroundY));
            yield return new WaitForFixedUpdate();
        }

        _hitbox.Deactivate();
        Boss.SetTrigger(_stopTrigger);
        CameraShake.Shake(_stopShake);

        yield return new WaitForSeconds(_recoverTime);
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
