using System.Collections;
using UnityEngine;

/// <summary>
/// 돌진 (2페이즈). 준비 → 시작 → 반복(돌진) → 끝 → 회복처럼 여러 상태로 나뉜 애니메이션 기준.
/// - 반복 클립이 재생되기 시작하면 바라보는 방향으로 맵 끝까지 돌진 (판정 켜짐)
/// - 맵 끝에 도착하면 판정을 끄고 종료 트리거를 보내 반복 상태를 빠져나감
/// - 끝 / 회복 애니메이션이 끝나면 패턴 종료
/// 반복 클립을 지정하지 않으면 판정 시작 / 끝 비율 구간 동안 돌진 (기존 방식).
/// 판정 높이를 2층 점프로 피할 수 있게 배치.
/// </summary>
public class BossChargePattern : BossPattern
{
    private const float LoopStartTimeout = 3f;
    private const float AnimationEndTimeout = 3f;

    [Header("돌진")]
    [Tooltip("돌진 판정 (보스 몸 앞쪽)")]
    [SerializeField]
    private BossHitbox _hitbox;

    [Tooltip("돌진하는 동안 반복 재생되는 클립 (예: Boss_tackle_loop). 이 클립이 시작되면 돌진 시작")]
    [SerializeField]
    private AnimationClip _loopClip;

    [Tooltip("돌진이 끝났을 때 반복 상태를 빠져나가기 위해 보낼 트리거 (애니메이터의 loop → end 전이 조건)")]
    [SerializeField]
    private string _loopEndTrigger = "tackleEnd";

    [Tooltip("돌진 속도 (초당 거리). 반복 클립을 쓸 때 사용")]
    [SerializeField, Min(0.1f)]
    private float _chargeSpeed = 14f;

    [Tooltip("맵 끝에서 이만큼 앞에서 멈춤")]
    [SerializeField, Min(0f)]
    private float _edgeMargin = 2f;

    [Tooltip("맵 정보가 없을 때 돌진 거리")]
    [SerializeField, Min(0f)]
    private float _fallbackDistance = 12f;

    [SerializeField] private CameraShakeSettings _stopShake = new CameraShakeSettings(0.3f, 0.2f);

    private float _direction;

    // 반복 클립을 쓰면 길이가 정해지지 않으므로 애니메이션 종료로 패턴 끝을 판단
    protected override bool WaitForDurationAfterExecute => _loopClip == null;

    // 돌진 판정이 몸통과 겹치므로 기본으로 몸통 접촉 데미지 끔 (기존 컴포넌트에도 적용)
    public BossChargePattern()
    {
        _disableContactDamage = true;
    }

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
        // 돌진 패턴 동안은 땅에 붙어서 정해진 방향으로만 움직임 (점프 / 방향 전환 없음)
        float startX = Boss.Position.x;
        float targetX = GetTargetX(startX, _direction);
        Boss.FaceDirection(_direction);
        Boss.ResetTrigger(_loopEndTrigger);

        if (_loopClip != null)
        {
            yield return ChargeWithLoopClip(startX, targetX);
        }
        else
        {
            yield return ChargeByRatio(startX, targetX);
        }
    }

    private IEnumerator ChargeWithLoopClip(float startX, float targetX)
    {
        // 예고: 반복(돌진) 클립이 시작될 때까지 제자리
        _hitbox.SetTelegraph(true);

        float waitUntil = Time.time + LoopStartTimeout;
        while (!Boss.IsPlayingClip(_loopClip) && Time.time < waitUntil)
        {
            Boss.MoveTo(new Vector2(startX, Boss.GroundY));
            yield return new WaitForFixedUpdate();
        }

        if (!Boss.IsPlayingClip(_loopClip))
        {
            Debug.LogWarning($"{name}: 돌진 반복 클립({_loopClip.name})이 재생되지 않아 바로 돌진합니다. 애니메이터 전이를 확인하세요.", this);
        }

        // 돌진: 일정 속도로 맵 끝까지
        _hitbox.SetTelegraph(false);
        _hitbox.Activate();

        float x = startX;
        float maxTime = (Mathf.Abs(targetX - startX) / _chargeSpeed) + 1f;
        float elapsed = 0f;

        while (!Mathf.Approximately(x, targetX) && elapsed < maxTime)
        {
            elapsed += Time.fixedDeltaTime;
            x = Mathf.MoveTowards(x, targetX, _chargeSpeed * Time.fixedDeltaTime);
            Boss.MoveTo(new Vector2(x, Boss.GroundY));
            yield return new WaitForFixedUpdate();
        }

        // 정지: 판정 끄고 반복 상태 종료
        _hitbox.Deactivate();
        Boss.SetTrigger(_loopEndTrigger);
        CameraShake.Shake(_stopShake);

        // 끝 / 회복 애니메이션이 끝날 때까지 그 자리에 고정
        // (트리거를 보낸 직후에는 아직 반복 클립이라 바로 끝난 것으로 보지 않도록 잠깐 기다림)
        yield return new WaitForFixedUpdate();

        waitUntil = Time.time + AnimationEndTimeout;
        while (IsPlayingPatternClip() && Time.time < waitUntil)
        {
            Boss.MoveTo(new Vector2(targetX, Boss.GroundY));
            yield return new WaitForFixedUpdate();
        }

        Boss.ResetTrigger(_loopEndTrigger);
    }

    private IEnumerator ChargeByRatio(float startX, float targetX)
    {
        // 예고: 제자리
        _hitbox.SetTelegraph(true);
        while (Progress < HitStartRatio)
        {
            Boss.MoveTo(new Vector2(startX, Boss.GroundY));
            yield return new WaitForFixedUpdate();
        }

        // 돌진: 판정 구간 동안 맵 끝까지
        _hitbox.SetTelegraph(false);
        _hitbox.Activate();

        float span = Mathf.Max(0.0001f, HitEndRatio - HitStartRatio);
        while (Progress < HitEndRatio)
        {
            float t = Mathf.Clamp01((Progress - HitStartRatio) / span);
            Boss.MoveTo(new Vector2(Mathf.Lerp(startX, targetX, t), Boss.GroundY));
            yield return new WaitForFixedUpdate();
        }

        _hitbox.Deactivate();
        CameraShake.Shake(_stopShake);

        // 정지 후 애니메이션이 끝날 때까지 그 자리에 고정
        while (Progress < 1f)
        {
            Boss.MoveTo(new Vector2(targetX, Boss.GroundY));
            yield return new WaitForFixedUpdate();
        }
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
