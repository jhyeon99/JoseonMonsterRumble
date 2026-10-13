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
    private GameObject _vfxInstance;
    private BossPoisonBeam _beam;

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

    public override bool CanUse() => base.CanUse() && _beam != null && _beam.IsReady;

    protected override IEnumerator Execute()
    {
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
        // 정상 종료에서는 입에 연결된 감압과 낙하 파편이 끝날 때까지 기다린다.
        while (_beam.IsFiring)
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
        if (_beam != null) _beam.StopFire();
    }

    private void OnDisable() => Stop();

    private void OnDestroy()
    {
        if (_vfxInstance != null) Destroy(_vfxInstance);
    }
}
