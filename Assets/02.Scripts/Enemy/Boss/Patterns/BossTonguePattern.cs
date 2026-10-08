using System.Collections;
using UnityEngine;

/// <summary>
/// 혓바닥 공격. 2층용 / 3층용으로 컴포넌트를 두 개 붙이고 각각 판정과 층을 지정.
/// 애니메이션 시작 ~ 판정 시작: 예고 / 판정 시작 ~ 끝: 혀 판정
/// 혀 길이 늘이기를 켜면 판정(BoxCollider2D)이 혀를 따라 입에서 끝까지 늘었다 줄어듦.
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

    private BoxCollider2D _box;
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

    protected override IEnumerator Execute()
    {
        if (!_stretchHitbox || _box == null)
        {
            yield return RunHitWindow(_hitbox);
            yield break;
        }

        _hitbox.SetTelegraph(true);
        yield return WaitUntilProgress(HitStartRatio);

        _hitbox.SetTelegraph(false);
        ApplyExtension(0f);
        _hitbox.Activate();

        float span = Mathf.Max(0.0001f, HitEndRatio - HitStartRatio);
        while (Progress < HitEndRatio)
        {
            float t = Mathf.Clamp01((Progress - HitStartRatio) / span);
            ApplyExtension(_extendCurve.Evaluate(t));
            yield return null;
        }

        _hitbox.Deactivate();
        RestoreCollider();
    }

    protected override void Cleanup()
    {
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

        // 최대로 뻗었을 때 판정 범위
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = t.localToWorldMatrix;
        Gizmos.DrawWireCube(new Vector3((_rootX + _tipX) * 0.5f, offsetY, 0f), new Vector3(Mathf.Abs(_tipX - _rootX), thickness, 0f));
        Gizmos.matrix = previous;
    }
}
