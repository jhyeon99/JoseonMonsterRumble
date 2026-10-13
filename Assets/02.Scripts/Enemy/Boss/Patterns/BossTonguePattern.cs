using System.Collections;
using UnityEngine;

/// <summary>
/// 혓바닥 공격. 2층용 / 3층용으로 컴포넌트를 두 개 붙이고 각각 판정과 층을 지정.
/// 애니메이션 시작 ~ 판정 시작: 예고 / 판정 시작 ~ 끝: 혀 판정
/// 혀 길이 늘이기를 켜면 판정(BoxCollider2D)이 혀를 따라 입에서 끝까지 늘었다 줄어듦.
/// 패링당하면 그 지점부터 애니메이션을 역재생해 혀를 바로 거둬들이고, 다 감기면 패턴 종료.
/// </summary>
public class BossTonguePattern : BossPattern
{
    [Header("혓바닥")]
    [Tooltip("혀 판정 (보스 자식, 해당 층 높이에 배치). BoxCollider2D 필요")]
    [SerializeField]
    private BossHitbox _hitbox;

    [Header("혀 길이 (판정이 혀를 따라 늘어남)")]
    [Tooltip("켜면 판정 구간 동안 아래 커브대로 판정 길이가 변함. 끄면 판정 크기 고정")]
    [SerializeField]
    private bool _stretchHitbox = true;

    [Tooltip("혀 뿌리(입) 위치. 판정 오브젝트 기준 로컬 X. 대각선이면 판정 오브젝트를 회전시켜 혀 방향을 로컬 X축에 맞추기")]
    [SerializeField]
    private float _rootX = 0f;

    [Tooltip("혀를 끝까지 뻗었을 때 끝 위치. 판정 오브젝트 기준 로컬 X")]
    [SerializeField]
    private float _tipX = -6f;

    [Tooltip("판정 구간(가로 0~1) 동안 혀 길이(세로 0~1). 기본: 빠르게 뻗고 잠깐 유지 후 회수")]
    [SerializeField]
    private AnimationCurve _extendCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.35f, 1f),
        new Keyframe(0.65f, 1f),
        new Keyframe(1f, 0f)
    );

    [Tooltip("혀가 이 길이보다 짧으면 판정 없음 (거의 안 나왔을 때)")]
    [SerializeField, Min(0f)]
    private float _minLength = 0.2f;

    [Header("사정거리")]
    [Tooltip("켜면 혀를 끝까지 뻗었을 때 닿는 거리(Tip X 기준) 안에 플레이어가 있을 때만 사용")]
    [SerializeField]
    private bool _useTongueLengthAsRange = true;

    [Tooltip("혀 길이 기준 사정거리에 더하는 여유 (월드 거리). 음수면 더 가까워야 사용")]
    [SerializeField]
    private float _rangeMargin = 0f;

    [Tooltip("혀 길이를 쓰지 않을 때 사정거리 (보스 중심에서 플레이어까지 가로 거리). 0이면 거리 제한 없음")]
    [SerializeField, Min(0f)]
    private float _manualRange = 0f;

    [Header("패링 시 혀 회수 (역재생)")]
    [Tooltip("켜면 패링당한 지점부터 애니메이션을 거꾸로 재생해 혀를 거둬들이고, 처음까지 감기면 패턴 종료")]
    [SerializeField]
    private bool _retractOnParry = true;

    [Tooltip("역재생 속도 (1 = 원래 클립 속도, 2 = 두 배 빠르게)")]
    [SerializeField, Min(0.1f)]
    private float _retractSpeed = 1f;

    [Tooltip("역재생이 끝난 뒤 재생할 애니메이터 상태 이름 (보통 대기 상태)")]
    [SerializeField]
    private string _retractEndState = "Idle";

    private BoxCollider2D _box;
    private bool _parried;
    private bool _retracted;
    private Vector2 _originalSize;
    private Vector2 _originalOffset;

    private void Reset()
    {
        SetDefaults("혓바닥 (2층)", BossPhaseMask.Both, BossFloorMask.Floor2);
    }

    protected override void OnInitialize()
    {
        if (_hitbox == null)
        {
            Debug.LogWarning($"{name}: {PatternName}에 판정(BossHitbox)이 연결되지 않아 사용하지 않습니다.", this);
            enabled = false;
            return;
        }

        _hitbox.OnParriedByPlayer += HandleParried;

        _box = _hitbox.GetComponent<BoxCollider2D>();
        if (_box != null)
        {
            _originalSize = _box.size;
            _originalOffset = _box.offset;
        }
        else if (_stretchHitbox)
        {
            Debug.LogWarning($"{name}: 혀 판정에 BoxCollider2D가 없어 길이 늘이기를 사용하지 않습니다.", this);
        }
    }

    /// <summary>층 / 쿨타임 조건에 더해, 플레이어가 혀 사정거리 안에 있을 때만 사용</summary>
    public override bool CanUse()
    {
        if (!base.CanUse())
        {
            return false;
        }

        float range = GetRange();
        if (range <= 0f || Boss.Player == null)
        {
            return true;
        }

        // 패턴 시작 때 플레이어 쪽으로 돌아보므로 방향과 관계없이 가로 거리만 비교
        float distance = Mathf.Abs(Boss.Player.position.x - Boss.Position.x);
        return distance <= range;
    }

    /// <summary>사정거리 (보스 중심 → 혀 끝 가로 거리 + 여유). 0이면 제한 없음</summary>
    private float GetRange()
    {
        if (_useTongueLengthAsRange && _hitbox != null)
        {
            Vector3 tip = _hitbox.transform.TransformPoint(new Vector3(_tipX, 0f, 0f));
            return Mathf.Max(0f, Mathf.Abs(tip.x - Boss.Position.x) + _rangeMargin);
        }

        return _manualRange;
    }

    private void OnDestroy()
    {
        if (_hitbox != null)
        {
            _hitbox.OnParriedByPlayer -= HandleParried;
        }
    }

    private void HandleParried()
    {
        _parried = true;
    }

    protected override void OnPatternStart()
    {
        base.OnPatternStart();
        _parried = false;
        _retracted = false;
    }

    /// <summary>역재생으로 끝냈으면 남은 패턴 길이를 기다리지 않음</summary>
    protected override bool WaitForDurationAfterExecute => !_retracted;

    protected override IEnumerator Execute()
    {
        bool stretch = _stretchHitbox && _box != null;

        _hitbox.SetTelegraph(true);
        yield return WaitUntilProgress(HitStartRatio);

        _hitbox.SetTelegraph(false);
        if (stretch)
        {
            ApplyExtension(0f);
        }

        _parried = false;
        _hitbox.Activate();

        float span = Mathf.Max(0.0001f, HitEndRatio - HitStartRatio);
        while (Progress < HitEndRatio && !_parried)
        {
            if (stretch)
            {
                float t = Mathf.Clamp01((Progress - HitStartRatio) / span);
                ApplyExtension(_extendCurve.Evaluate(t));
            }

            yield return null;
        }

        _hitbox.Deactivate();
        RestoreCollider();

        if (_parried && _retractOnParry)
        {
            yield return Retract();
        }
    }

    /// <summary>패링당한 지점부터 애니메이션을 거꾸로 재생해 혀를 거둬들임</summary>
    private IEnumerator Retract()
    {
        AnimationClip clip = GetPlayingPatternClip();
        if (clip == null || !Boss.TryGetClipState(clip, out int stateHash, out float time))
        {
            yield break;
        }

        _retracted = true;
        float rate = _retractSpeed / Mathf.Max(0.01f, clip.length);
        Boss.PoseAnimator(stateHash, time);

        while (time > 0f)
        {
            yield return null;
            time = Mathf.Max(0f, time - (rate * Time.deltaTime));
            Boss.PoseAnimator(stateHash, time);
        }

        Boss.ResumeAnimator();

        if (!Boss.PlayState(_retractEndState))
        {
            Debug.LogWarning($"{name}: {PatternName} 역재생 후 재생할 상태({_retractEndState})가 애니메이터에 없습니다. Retract End State 이름을 확인하세요.", this);
        }
    }

    protected override void Cleanup()
    {
        // 역재생 도중 그로기 / 페이즈 전환으로 끊겨도 애니메이터가 멈춘 채 남지 않도록
        if (Boss != null)
        {
            Boss.ResumeAnimator();
        }

        if (_hitbox != null)
        {
            _hitbox.Deactivate();
            _hitbox.SetTelegraph(false);
        }

        RestoreCollider();
    }

    /// <summary>혀 길이 비율(0~1)에 맞춰 판정을 입에서 끝 방향으로 늘림</summary>
    private void ApplyExtension(float amount)
    {
        float fullLength = Mathf.Abs(_tipX - _rootX);
        float length = fullLength * Mathf.Clamp01(amount);
        float direction = _tipX >= _rootX ? 1f : -1f;

        // 너무 짧을 때는 입 안쪽에 아주 작게 두어 사실상 판정 없음
        if (length < _minLength)
        {
            _box.size = new Vector2(0.01f, 0.01f);
            _box.offset = new Vector2(_rootX, _originalOffset.y);
            return;
        }

        _box.size = new Vector2(length, _originalSize.y);
        _box.offset = new Vector2(_rootX + (direction * length * 0.5f), _originalOffset.y);
    }

    private void RestoreCollider()
    {
        if (_box == null)
        {
            return;
        }

        _box.size = _originalSize;
        _box.offset = _originalOffset;
    }

    private void OnDrawGizmosSelected()
    {
        if (_hitbox == null || !_stretchHitbox)
        {
            return;
        }

        BoxCollider2D box = _hitbox.GetComponent<BoxCollider2D>();
        float thickness = box != null ? box.size.y : 0.5f;
        float offsetY = box != null ? box.offset.y : 0f;
        Transform t = _hitbox.transform;

        Vector3 root = t.TransformPoint(new Vector3(_rootX, offsetY, 0f));
        Vector3 tip = t.TransformPoint(new Vector3(_tipX, offsetY, 0f));

        // 혀 뿌리 → 끝 (최대 길이)
        Gizmos.color = new Color(1f, 0.3f, 0.6f, 0.9f);
        Gizmos.DrawLine(root, tip);
        Gizmos.DrawWireSphere(root, 0.15f);
        Gizmos.DrawWireSphere(tip, 0.15f);

        // 사정거리 (보스 중심 기준 좌우)
        Boss boss = GetComponentInParent<Boss>();
        if (boss != null && _useTongueLengthAsRange)
        {
            float range = Mathf.Abs(tip.x - boss.transform.position.x) + _rangeMargin;
            Vector3 center = boss.transform.position;
            Gizmos.color = new Color(1f, 0.3f, 0.6f, 0.35f);
            Gizmos.DrawLine(center + Vector3.left * range + Vector3.down * 0.5f, center + Vector3.left * range + Vector3.up * 3f);
            Gizmos.DrawLine(center + Vector3.right * range + Vector3.down * 0.5f, center + Vector3.right * range + Vector3.up * 3f);
            Gizmos.color = new Color(1f, 0.3f, 0.6f, 0.9f);
        }

        // 최대로 뻗었을 때 판정 범위
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = t.localToWorldMatrix;
        Gizmos.DrawWireCube(new Vector3((_rootX + _tipX) * 0.5f, offsetY, 0f), new Vector3(Mathf.Abs(_tipX - _rootX), thickness, 0f));
        Gizmos.matrix = previous;
    }
}
