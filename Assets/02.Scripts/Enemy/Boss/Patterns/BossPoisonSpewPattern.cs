using System.Collections;
using UnityEngine;

/// <summary>기존 BossPattern 규약으로 독분사를 실행한다. 실제 보스 등록은 접목 때 수행한다.</summary>
[DisallowMultipleComponent]
public sealed class BossPoisonSpewPattern : BossPattern
{
    [Header("독분사 연결")]
    [Tooltip("비활성 상태로 저장된 PF_BossPoisonBeam 프리팹")]
    [SerializeField] private GameObject _vfxPrefab;
    [Tooltip("보스 Animator가 독분사 클립에서 움직이는 입 기준점")]
    [SerializeField] private Transform _mouth;
    [SerializeField] private SpriteRenderer _bossRenderer;
    [SerializeField, Range(0f, 1f)] private float _followThroughLeadRatio = 0.176f;

    [Header("입 방향 보정 (클립 시간에 따라 시작 → 끝으로 변함)")]
    [Tooltip("분사 시작 쪽 각도 보정 (도). 음수일수록 아래로. 프리뷰 클립 기준 -20")]
    [SerializeField] private float _startAngleOffset = -20f;
    [Tooltip("보정이 끝나는 쪽 각도 보정 (도)")]
    [SerializeField] private float _endAngleOffset = 0f;
    [Tooltip("분사 시작 쪽 입술 위치 보정 (입 기준 로컬)")]
    [SerializeField] private Vector2 _startLipOffset = new Vector2(-0.36f, -0.1f);
    [Tooltip("보정이 끝나는 쪽 입술 위치 보정")]
    [SerializeField] private Vector2 _endLipOffset = Vector2.zero;
    [Tooltip("시작 → 끝 보정이 다 바뀌는 클립 시간 (초). 프리뷰 클립 기준 2.3125")]
    [SerializeField, Min(0.01f)] private float _offsetBlendEndTime = 2.3125f;

    [Header("사용 조건")]
    [Tooltip("플레이어와 가로 거리가 이 값 이상일 때만 사용 (월드). 가까우면 후보에서 빠지고, 콤보에서는 대체 패턴(Fallback)으로 넘어감. 0이면 거리 무시")]
    [SerializeField, Min(0f)] private float _minPlayerDistance = 5f;
    private GameObject _vfxInstance;
    private BossPoisonBeam _beam;
    // 정상 종료면 남은 방울 / 파편은 VFX가 혼자 마무리하도록 끄지 않음 (끊겼을 때만 즉시 끔)
    private bool _completedNormally;

    private void Reset() => SetDefaults("독분사", BossPhaseMask.Both, BossFloorMask.Any);

    protected override void OnInitialize()
    {
        if (_beam != null) return;
        if (_vfxPrefab == null || _mouth == null || _bossRenderer == null || _vfxPrefab.activeSelf)
        {
            Debug.LogWarning("독분사 패턴: 비활성 VFX 프리팹, 입 또는 보스 SpriteRenderer 연결을 확인하세요.", this);
            return;
        }
        _vfxInstance = Instantiate(_vfxPrefab, Boss.transform);
        _beam = _vfxInstance.GetComponentInChildren<BossPoisonBeam>(true);
        if (_beam == null || !_beam.SetEmitter(_mouth, _bossRenderer, Boss.gameObject))
        {
            Debug.LogWarning("독분사 패턴: VFX의 BossPoisonBeam 연결에 실패했습니다.", this);
            Destroy(_vfxInstance);
            _vfxInstance = null;
            _beam = null;
            return;
        }
        _vfxInstance.SetActive(true);
    }

    public override bool CanUse() => base.CanUse() && _beam != null && _beam.IsReady && IsPlayerFarEnough();

    /// <summary>플레이어가 Min Player Distance 이상 떨어져 있는지 (가로 거리 기준)</summary>
    private bool IsPlayerFarEnough()
    {
        if (_minPlayerDistance <= 0f)
        {
            return true;
        }

        Transform player = Boss.Player;
        if (player == null)
        {
            return false;
        }

        return Mathf.Abs(player.position.x - Boss.Position.x) >= _minPlayerDistance;
    }

    protected override IEnumerator Execute()
    {
        _completedNormally = false;
        if (_beam == null || !_beam.IsReady || GetPlayingPatternClip() == null)
        {
            Debug.LogWarning("독분사 패턴: 연결한 패턴 클립이 실제 Animator에서 재생되는지 확인하세요.", this);
            yield break;
        }
        while (Progress < HitStartRatio)
        {
            ApplyEmitterOffsets();
            yield return null;
        }
        ApplyEmitterOffsets();
        _beam.StartFire();
        while (Progress < HitEndRatio)
        {
            ApplyEmitterOffsets();
            if (Progress >= Mathf.Max(HitStartRatio, HitEndRatio - _followThroughLeadRatio)) _beam.StartFollowThrough();
            yield return null;
        }
        _beam.StopEmission();
        // 패턴은 애니메이션 길이까지만 기다리고, 감압 / 떨어지는 방울은 VFX가 이어서 처리
        // (방울 수명(Residual Particle Lifetime)이 길면 그동안 보스가 패턴에 묶여 멈춰 있던 문제)
        _completedNormally = true;
        while (_beam.IsFiring && Progress < 1f)
        {
            ApplyEmitterOffsets();
            yield return null;
        }
    }

    /// <summary>클립 시간에 맞춰 입 방향 / 입술 위치 보정을 시작 → 끝으로 넣어 줌 (프리뷰 클립의 보정 키 재현)</summary>
    private void ApplyEmitterOffsets()
    {
        if (_beam == null) return;
        // 클립이 끝나 다른 상태로 넘어갔으면 마지막 보정 유지
        AnimationClip clip = GetPlayingPatternClip();
        if (clip == null) return;
        float clipTime = Progress * clip.length;
        float t = Mathf.Clamp01(clipTime / _offsetBlendEndTime);
        _beam.SetEmitterOffsets(Mathf.Lerp(_startAngleOffset, _endAngleOffset, t), Vector2.Lerp(_startLipOffset, _endLipOffset, t));
    }

    protected override void Cleanup()
    {
        if (_beam != null && !_completedNormally) _beam.StopFire();
        _completedNormally = false;
    }

    private void OnDisable() => Stop();

    private void OnDestroy()
    {
        if (_vfxInstance != null) Destroy(_vfxInstance);
    }
}
