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
        if (_beam == null) return;
        if (_beamParticleMaterial == null)
        {
            Debug.LogWarning("독 빔 테스트: 파티클 Material이 없습니다.", this);
            return;
        }
        _beam.SetParticles(CreateBeamParticles("MouthParticles", false), CreateBeamParticles("HeadParticles", true),
            CreateImpactCloudParticles());
    }

    private ParticleSystem CreateImpactCloudParticles()
    {
        ParticleSystem particles = CreateBeamParticles("ImpactCloudParticles", true);
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.gravityModifier = 0.15f;
        main.maxParticles = 256;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = false;
        ParticleSystem.CollisionModule collision = particles.collision;
        collision.enabled = false;
        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.35f), new Keyframe(0.2f, 1.35f), new Keyframe(1f, 1.1f)));
        if (_impactCloudMaterial != null)
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = _impactCloudMaterial;
        else
            Debug.LogWarning("독분사 테스트: 충돌 덩어리 Material이 없어 방울 Material을 사용합니다.", this);
        return particles;
    }

    private ParticleSystem CreateBeamParticles(string name, bool isHead)
    {
        var particleObject = new GameObject(name);
        particleObject.transform.SetParent(_beam.transform, false);
        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, isHead ? 0.35f : 0.22f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(isHead ? 1.5f : 3f, isHead ? 5f : 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, isHead ? 0.3f : 0.18f);
        main.startColor = Color.white;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0.92f;
        main.maxParticles = isHead ? 2048 : 256;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = isHead ? 65f : 45f;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = isHead ? 55f : 18f;
        shape.radius = isHead ? 0.35f : 0.12f;
        shape.rotation = new Vector3(0f, isHead ? -90f : 90f, 0f);
        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
        if (isHead)
        {
            ParticleSystem.CollisionModule collision = particles.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision2D;
            collision.collidesWith = Constants.Layers.GroundMask;
            collision.bounce = 0f;
            collision.lifetimeLoss = 1f;
        }
        particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = _beamParticleMaterial;
        return particles;
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
