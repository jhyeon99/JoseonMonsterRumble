using UnityEngine;

/// <summary>Plays only the isolated clip with frame-keyed Mouth transform.</summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class BossPoisonSpewPreview : MonoBehaviour
{
    private static readonly int SpewStateHash = Animator.StringToHash("PoisonSpewPreview");

    [SerializeField] private Animator _animator;
    [SerializeField] private SpriteRenderer _bossRenderer;
    [SerializeField] private AnimationClip _spewClip;
    [SerializeField] private PoisonStream2D _stream;
    [SerializeField] private BossPoisonBeam _beam;
    [SerializeField] private Material _beamParticleMaterial;
    [SerializeField] private Material _impactCloudMaterial;
    [SerializeField] private Transform _muzzle;
    [SerializeField, Min(0)] private int _startFrame = 10;
    [SerializeField, Min(0)] private int _stopFrame = 38;
    [SerializeField, Min(0f)] private float _pauseDuration = 2f;
    [SerializeField, Min(0f)] private float _followThroughDuration = 0.55f;
    private float _cycleTime;
    private bool _isEmitting;

    private void Awake()
    {
        if (_beam != null) BossPoisonParticleFactory.InitializeParticles(_beam, _beamParticleMaterial, _impactCloudMaterial);
    }

    private void Start()
    {
        if (_animator == null || _bossRenderer == null || _spewClip == null
            || (_stream == null && _beam == null) || _muzzle == null)
        {
            Debug.LogWarning("보스 독분사 테스트: 임시 클립, 입 기준점 또는 독줄기 참조가 없습니다.", this);
            enabled = false;
            return;
        }
        _animator.speed = 0f;
        if (_beam == null)
        {
            _stream.SetMuzzle(_muzzle);
            _stream.SetSortingBehind(_bossRenderer);
        }
        UpdateAnimationAndEmission();
    }

    private void Update()
    {
        _cycleTime += Time.deltaTime;
        float cycleDuration = _spewClip.length + _pauseDuration;
        if (_cycleTime >= cycleDuration)
        {
            StopEmission();
            _isEmitting = false;
            _cycleTime %= cycleDuration;
        }
        UpdateAnimationAndEmission();
    }

    private void UpdateAnimationAndEmission()
    {
        float animationTime = Mathf.Min(_cycleTime, _spewClip.length - 1f / _spewClip.frameRate);
        _animator.Play(SpewStateHash, 0, animationTime / _spewClip.length);
        _animator.Update(0f);
        int frame = Mathf.FloorToInt(animationTime * _spewClip.frameRate);
        bool shouldEmit = _cycleTime < _spewClip.length && frame >= _startFrame && frame < _stopFrame;
        if (shouldEmit && _isEmitting && _beam != null
            && animationTime >= _stopFrame / _spewClip.frameRate - _followThroughDuration)
        {
            _beam.StartFollowThrough();
        }

        if (shouldEmit == _isEmitting)
            return;
        _isEmitting = shouldEmit;
        if (_isEmitting)
        {
            if (_beam != null)
            {
                _beam.StartFire();
            }
            else
            {
                _stream.StartEmission();
            }
        }
        else
        {
            StopEmission();
        }
    }

    private void StopEmission()
    {
        if (_beam != null)
            _beam.StopEmission();
        else if (_stream != null)
            _stream.StopEmission();
    }

    private void OnDisable()
    {
        if (_beam != null)
            _beam.StopFire();
        else
            StopEmission();
        _isEmitting = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (_muzzle == null)
            return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(_muzzle.position, 0.12f);
        Gizmos.DrawLine(_muzzle.position, _muzzle.position + _muzzle.right);
    }
}
