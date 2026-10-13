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
        yield return WaitUntilProgress(HitStartRatio);
        _beam.StartFire();
        while (Progress < HitEndRatio)
        {
            if (Progress >= Mathf.Max(HitStartRatio, HitEndRatio - _followThroughLeadRatio)) _beam.StartFollowThrough();
            yield return null;
        }
        _beam.StopEmission();
        // 정상 종료에서는 입에 연결된 감압과 낙하 파편이 끝날 때까지 기다린다.
        while (_beam.IsFiring) yield return null;
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
