using System.Collections;
using UnityEngine;

/// <summary>
/// 꼬리치며 뒤돌기 (1, 2층). 플레이어가 보스 뒤쪽에 있을 때 몸을 돌리며 꼬리로 등 뒤를 쓸어 침.
/// - 패턴 시작 시 플레이어 쪽으로 돌지 않음 (꼬리가 플레이어를 향하도록 등을 보인 채 시작)
/// - 판정 시작 ~ 끝: 꼬리 판정 / Turn Ratio 지점: 플레이어 쪽으로 몸 방향 전환
/// - 애니메이터 전이 조건(isPlayerBehind)을 패턴이 직접 켜고 끔
/// Animation Trigger에 "tail", Animation Clip에 꼬리 클립을 넣을 것
/// </summary>
public class BossTailPattern : BossPattern
{
    [Header("꼬리")]
    [Tooltip("꼬리 판정들 (보스 자식, 보스 등 뒤쪽에 배치). 1층 / 2층을 따로 만들었으면 둘 다 넣기. 판정 구간 동안 전부 켜짐")]
    [SerializeField]
    private BossHitbox[] _hitboxes;

    [Tooltip("판정 시작 전까지 예고 표시(BossHitbox의 Telegraph)를 보여줌")]
    [SerializeField]
    private bool _showTelegraph = false;

    [Header("사용 조건 (플레이어가 보스 등 뒤에 있을 때)")]
    [Tooltip("켜면 플레이어가 꼬리 판정(Hitboxes) 범위 안에 있을 때만 사용 (혓바닥처럼). 끄면 아래 등 뒤 가로 거리로 판단")]
    [SerializeField]
    private bool _useHitboxAsRange = true;

    [Tooltip("판정 범위에 더하는 여유 (x: 가로, y: 세로). 조금 밖에 있어도 쓰게 하려면 양수")]
    [SerializeField]
    private Vector2 _rangeMargin = Vector2.zero;

    [Tooltip("(Use Hitbox As Range를 끈 경우) 등 뒤로 이 거리보다 가까우면 사용 안 함 (0이면 제한 없음)")]
    [SerializeField, Min(0f)]
    private float _minDistance = 0f;

    [Tooltip("(Use Hitbox As Range를 끈 경우) 등 뒤로 이 거리 안에 있을 때만 사용 (꼬리가 닿는 거리 정도)")]
    [SerializeField, Min(0f)]
    private float _maxDistance = 6f;

    [Tooltip("켜면 패턴이 끝났을 때 플레이어가 등 뒤 꼬리 범위 안에 있으면, 돌아보지 않고 대기 시간 없이 바로 꼬리치기로 돌아섬. " +
             "끄면 다른 패턴처럼 대기 후 가중치 랜덤으로 선택")]
    [SerializeField]
    private bool _useImmediatelyWhenBehind = true;

    [Header("뒤돌기")]
    [Tooltip("몸 방향을 플레이어 쪽으로 바꾸는 지점 (애니메이션 길이 대비 비율). 1 = 애니메이션이 끝날 때. " +
             "클립 안에서 몸이 돌아가는 그림이면 1, 중간에 스프라이트를 뒤집어야 하면 그 지점으로")]
    [SerializeField, Range(0f, 1f)]
    private float _turnRatio = 1f;

    [Header("애니메이터 파라미터")]
    [Tooltip("플레이어가 뒤에 있음을 알리는 Bool (애니메이터 전이 조건). 패턴 동안 켜고 끝나면 끔")]
    [SerializeField]
    private string _playerBehindBool = "isPlayerBehind";

    [Header("타격감")]
    [Tooltip("판정이 켜지는 순간 카메라 흔들림")]
    [SerializeField]
    private CameraShakeSettings _swingShake = new CameraShakeSettings(0.1f, 0.12f);

    private bool _turned;

    /// <summary>Turn Ratio = 1: 애니메이터가 꼬리 클립을 벗어나는 프레임에 돌아보기 대기 중</summary>
    private bool _turnPending;

    private void Reset()
    {
        SetDefaults("꼬리치며 뒤돌기", BossPhaseMask.Both, BossFloorMask.Floor1 | BossFloorMask.Floor2);
    }

    /// <summary>등 뒤 즉시 사용이 켜져 있으면, 조건이 맞을 때 다른 패턴보다 먼저 / 대기 없이 사용</summary>
    public override bool IsForced => _useImmediatelyWhenBehind;

    /// <summary>기본 조건(페이즈 / 층 / 쿨타임) + 플레이어가 등 뒤 사용 거리 안에 있을 때</summary>
    public override bool CanUse()
    {
        if (!base.CanUse() || Boss.Player == null)
        {
            return false;
        }

        // 즉시 사용 모드: 패턴이 끝난 직후 아직 돌아보기 전에만 사용 (이미 플레이어를 돌아봤으면 꼬리를 쓰지 않음)
        bool hasTurned = Mathf.Sign(Boss.FacingX) != Mathf.Sign(Boss.IdleEnterFacingX);
        if (_useImmediatelyWhenBehind && hasTurned)
        {
            return false;
        }

        // 대기 중에는 계속 플레이어를 보므로, 대기에 들어오기 직전에 보던 방향 기준으로 등 뒤인지 판단
        float toPlayer = Boss.Player.position.x - Boss.Position.x;
        bool isBehind = Mathf.Sign(toPlayer) != Mathf.Sign(Boss.IdleEnterFacingX) && Mathf.Abs(toPlayer) > 0.01f;
        if (!isBehind)
        {
            return false;
        }

        // 판정 범위 기준. 대기 중에는 플레이어 쪽으로 돌아봐서 꼬리 판정이 반대편에 가 있으므로,
        // 지금 방향이 등을 보이던 방향과 다르면 판정 범위를 보스 기준으로 좌우 반전해서 검사
        if (_useHitboxAsRange && HasHitboxRect())
        {
            float? mirror = Mathf.Sign(Boss.FacingX) != Mathf.Sign(Boss.IdleEnterFacingX)
                ? Boss.Position.x
                : (float?)null;

            foreach (BossHitbox hitbox in _hitboxes)
            {
                if (hitbox != null && BossHitboxRange.ContainsPlayer(hitbox, Boss.Player, _rangeMargin, mirror))
                {
                    return true;
                }
            }

            return false;
        }

        float distance = Mathf.Abs(toPlayer);
        return distance >= _minDistance && distance <= _maxDistance;
    }

    private bool HasHitboxRect()
    {
        if (_hitboxes == null)
        {
            return false;
        }

        foreach (BossHitbox hitbox in _hitboxes)
        {
            if (BossHitboxRange.TryGetWorldRect(hitbox, out _))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>플레이어 쪽으로 돌지 않음 (등 = 꼬리가 플레이어를 향한 채 시작). 애니메이터 전이 조건 켜기</summary>
    protected override void OnPatternStart()
    {
        _turned = false;
        _turnPending = false;

        // 대기 중에 플레이어를 돌아봤어도, 등을 보이던 원래 방향으로 되돌려 꼬리가 플레이어를 향하게 함
        Boss.FaceDirection(Boss.IdleEnterFacingX);

        if (!string.IsNullOrEmpty(_playerBehindBool))
        {
            Boss.SetBool(_playerBehindBool, true);
        }
    }

    protected override IEnumerator Execute()
    {
        if (_showTelegraph)
        {
            SetTelegraph(true);
        }

        // 판정 시작까지 대기 (패링 그로기 판정이면 직전에 반짝임)
        bool hasWarned = false;
        BossHitbox warnHitbox = FirstHitbox();
        while (Progress < HitStartRatio)
        {
            hasWarned = UpdateParryWarning(warnHitbox, HitStartRatio, hasWarned);
            TryTurn();
            yield return null;
        }

        SetTelegraph(false);
        SetHitboxes(true);
        CameraShake.Shake(_swingShake);
        AudioAction("Boss.Tail");

        while (Progress < HitEndRatio)
        {
            TryTurn();
            yield return null;
        }

        SetHitboxes(false);

        // Turn Ratio = 1: 시간(Progress)으로 재면 애니메이터보다 한 프레임 빨리 돌 수 있으므로,
        // 애니메이터가 실제로 꼬리 클립을 벗어나는 프레임에 LateUpdate에서 돌아봄
        if (!_turned && _turnRatio >= 1f)
        {
            _turned = true;
            _turnPending = true;
            yield break;
        }

        // 뒤돌기 지점이 판정 뒤라면 거기까지 기다렸다가 돌기
        while (!_turned && Progress < _turnRatio)
        {
            yield return null;
        }

        TryTurn(force: true);
    }

    /// <summary>Turn Ratio에 도달하면 한 번만 플레이어 쪽으로 방향 전환</summary>
    private void TryTurn(bool force = false)
    {
        if (_turned || _turnRatio >= 1f || (!force && Progress < _turnRatio))
        {
            return;
        }

        _turned = true;
        Boss.FacePlayer();
    }

    /// <summary>
    /// LateUpdate는 애니메이터가 이번 프레임 스프라이트를 정한 뒤, 화면에 그리기 전에 호출됨.
    /// 여기서 꼬리 클립을 벗어난 걸 확인하고 돌아보면 대기 첫 프레임과 방향 전환이 같은 프레임에 보임
    /// (코루틴은 애니메이터보다 먼저 돌아서, 시간으로 재면 마지막 프레임이 남은 채 먼저 돌 수 있음)
    /// </summary>
    private void LateUpdate()
    {
        if (!_turnPending || Boss == null)
        {
            return;
        }

        if (Boss.HasAnimator && IsPlayingPatternClip())
        {
            return;
        }

        _turnPending = false;
        Boss.FacePlayer();
    }

    private BossHitbox FirstHitbox()
    {
        if (_hitboxes == null)
        {
            return null;
        }

        foreach (BossHitbox hitbox in _hitboxes)
        {
            if (hitbox != null)
            {
                return hitbox;
            }
        }

        return null;
    }

    private void SetHitboxes(bool active)
    {
        if (_hitboxes == null)
        {
            return;
        }

        foreach (BossHitbox hitbox in _hitboxes)
        {
            if (hitbox == null)
            {
                continue;
            }

            if (active)
            {
                hitbox.Activate();
            }
            else
            {
                hitbox.Deactivate();
            }
        }
    }

    private void SetTelegraph(bool show)
    {
        if (_hitboxes == null)
        {
            return;
        }

        foreach (BossHitbox hitbox in _hitboxes)
        {
            if (hitbox != null)
            {
                hitbox.SetTelegraph(show);
            }
        }
    }

    protected override void Cleanup()
    {
        SetHitboxes(false);
        SetTelegraph(false);

        // 돌아보기 전에 끝났으면(애니메이터 없음 등) 여기서 마저 돌아봄 (기존과 같은 결과)
        if (_turnPending && Boss != null)
        {
            _turnPending = false;
            Boss.FacePlayer();
        }

        if (Boss != null && !string.IsNullOrEmpty(_playerBehindBool))
        {
            Boss.SetBool(_playerBehindBool, false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_useHitboxAsRange && _hitboxes != null)
        {
            bool drawn = false;
            foreach (BossHitbox hitbox in _hitboxes)
            {
                if (BossHitboxRange.TryGetWorldRect(hitbox, out Rect rect))
                {
                    rect.xMin -= _rangeMargin.x;
                    rect.xMax += _rangeMargin.x;
                    rect.yMin -= _rangeMargin.y;
                    rect.yMax += _rangeMargin.y;
                    BossHitboxRange.DrawGizmo(rect, new Color(0.6f, 0.4f, 1f, 0.9f));
                    drawn = true;
                }
            }

            if (drawn)
            {
                return;
            }
        }

        // 등 뒤 사용 거리 (보스가 보는 반대쪽)
        Boss boss = GetComponentInParent<Boss>();
        float back = boss != null ? -boss.FacingX : 1f;
        Vector3 center = transform.position;

        Gizmos.color = new Color(0.6f, 0.4f, 1f, 0.8f);
        Gizmos.DrawLine(center, center + (Vector3.right * back * _maxDistance));

        if (_minDistance > 0f)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.8f);
            Gizmos.DrawLine(center + (Vector3.up * 0.2f), center + (Vector3.right * back * _minDistance) + (Vector3.up * 0.2f));
        }
    }
}
