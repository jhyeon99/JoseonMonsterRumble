using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Ballistic simulation with a merged 2D density surface. No terrain or damage logic.</summary>
[DisallowMultipleComponent]
public sealed class PoisonStream2D : MonoBehaviour
{
    private const int ParticleCapacity = 1024;
    private const int SurfacePointCapacity = 128;
    private const int MouthPointCount = 4;
    private const float EmissionInterval = 1f / 120f;
    private static readonly int MetaballsId = Shader.PropertyToID("_Metaballs");
    private static readonly int PointCountId = Shader.PropertyToID("_PointCount");
    private static readonly int FlowDirectionId = Shader.PropertyToID("_FlowDirection");
    private static readonly IComparer<ParticleSystem.Particle> ParticleAgeComparer =
        Comparer<ParticleSystem.Particle>.Create((a, b) => a.remainingLifetime.CompareTo(b.remainingLifetime));

    [SerializeField] private Material _material;
    [SerializeField] private Transform _muzzle;
    [SerializeField, Min(0.1f)] private float _speed = 14f;
    [SerializeField] private float _angleDegrees = 10f;
    [SerializeField, Min(0f)] private float _gravity = 9f;
    [SerializeField, Range(0.2f, 4f)] private float _lifetime = 2.8f;
    [SerializeField, Min(0.01f)] private float _width = 0.9f;
    [SerializeField, Range(0.1f, 1f)] private float _mouthWidthRatio = 0.4f;
    [SerializeField] private int _sortingOrder = 20;

    private readonly ParticleSystem.Particle[] _particles = new ParticleSystem.Particle[ParticleCapacity];
    private readonly Vector4[] _surfacePoints = new Vector4[SurfacePointCapacity];
    private readonly Vector3[] _quadVertices = new Vector3[4];
    private Mesh _mesh;
    private Material _runtimeMaterial;
    private MeshRenderer _renderer;
    private ParticleSystem _simulation;
    private bool _isEmitting;
    private float _simulationTime;
    private float _nextEmissionTime;
    private Vector2 _previousMuzzlePosition;
    private Vector2 _previousDirection;

    private void Awake()
    {
        if (_material == null)
        {
            Debug.LogWarning("독분사: 액체 표면 Material이 지정되지 않았습니다.", this);
            enabled = false;
            return;
        }
        var surface = new GameObject("MergedLiquidSurface", typeof(MeshFilter), typeof(MeshRenderer));
        surface.transform.SetParent(transform, false);
        _mesh = new Mesh { name = "PoisonMetaballSurface_Runtime" };
        _mesh.MarkDynamic();
        _mesh.vertices = _quadVertices;
        _mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        surface.GetComponent<MeshFilter>().sharedMesh = _mesh;
        _runtimeMaterial = new Material(_material);
        _renderer = surface.GetComponent<MeshRenderer>();
        _renderer.sharedMaterial = _runtimeMaterial;
        _renderer.sortingOrder = _sortingOrder;
        _renderer.shadowCastingMode = ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        _renderer.enabled = false;
        InitializeSimulation();
    }

    private void InitializeSimulation()
    {
        var simulationObject = new GameObject("LiquidSimulation");
        simulationObject.transform.SetParent(transform, false);
        _simulation = simulationObject.AddComponent<ParticleSystem>();
        _simulation.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = _simulation.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.startLifetime = _lifetime;
        main.startSpeed = 0f;
        main.gravityModifier = 0f;
        main.maxParticles = ParticleCapacity;
        ParticleSystem.EmissionModule emission = _simulation.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = _simulation.shape;
        shape.enabled = false;
        ParticleSystem.ForceOverLifetimeModule force = _simulation.forceOverLifetime;
        force.enabled = true;
        force.space = ParticleSystemSimulationSpace.World;
        force.y = -_gravity;
        simulationObject.GetComponent<ParticleSystemRenderer>().enabled = false;
    }

    public void SetMuzzle(Transform muzzle)
    {
        _muzzle = muzzle;
    }

    public void SetSortingBehind(SpriteRenderer reference)
    {
        if (_renderer == null || reference == null)
            return;
        _renderer.sortingLayerID = reference.sortingLayerID;
        _renderer.sortingOrder = reference.sortingOrder - 1;
    }

    public void StartEmission()
    {
        if (_simulation == null || _isEmitting)
            return;
        _isEmitting = true;
        _nextEmissionTime = _simulationTime;
        _previousMuzzlePosition = GetMuzzlePosition();
        _previousDirection = GetEmissionDirection();
        _simulation.Play();
    }

    public void StopEmission()
    {
        _isEmitting = false;
    }

    public void ResetStream()
    {
        _isEmitting = false;
        if (_simulation != null)
            _simulation.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_renderer != null)
            _renderer.enabled = false;
    }

    private Vector2 GetMuzzlePosition()
    {
        return _muzzle != null ? _muzzle.position : transform.position;
    }

    private Vector2 GetEmissionDirection()
    {
        Transform origin = _muzzle != null ? _muzzle : transform;
        float angle = _angleDegrees * Mathf.Deg2Rad;
        return origin.TransformVector(new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f)).normalized;
    }

    private void FixedUpdate()
    {
        float previousTime = _simulationTime;
        _simulationTime += Time.fixedDeltaTime;
        Vector2 muzzlePosition = GetMuzzlePosition();
        Vector2 direction = GetEmissionDirection();
        if (_isEmitting)
        {
            while (_nextEmissionTime <= _simulationTime)
            {
                float progress = Mathf.Clamp01((_nextEmissionTime - previousTime) / Time.fixedDeltaTime);
                Vector2 velocity = Vector2.Lerp(_previousDirection, direction, progress).normalized * _speed;
                float age = _simulationTime - _nextEmissionTime;
                Vector2 position = Vector2.Lerp(_previousMuzzlePosition, muzzlePosition, progress)
                    + velocity * age + Vector2.down * (0.5f * _gravity * age * age);
                var particle = new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = velocity + Vector2.down * (_gravity * age),
                    startLifetime = Mathf.Max(0.01f, _lifetime - age)
                };
                _simulation.Emit(particle, 1);
                _nextEmissionTime += EmissionInterval;
            }
        }
        _previousMuzzlePosition = muzzlePosition;
        _previousDirection = direction;
    }

    private void LateUpdate()
    {
        if (_simulation == null)
            return;
        int particleCount = _simulation.GetParticles(_particles);
        if (particleCount == 0 && !_isEmitting)
        {
            _renderer.enabled = false;
            return;
        }
        // Age ordering preserves uniform coverage when Unity recycles particle slots.
        Array.Sort(_particles, 0, particleCount, ParticleAgeComparer);
        int mouthCount = _isEmitting ? MouthPointCount : 0;
        int sampleCount = Mathf.Min(particleCount, SurfacePointCapacity - mouthCount);
        float radius = _width * 0.95f;
        for (int i = 0; i < sampleCount; i++)
        {
            int particleIndex = sampleCount > 1 ? i * (particleCount - 1) / (sampleCount - 1) : 0;
            Vector3 position = _particles[particleIndex].position;
            _surfacePoints[i] = new Vector4(position.x, position.y, radius, 0f);
        }
        Vector2 muzzlePosition = GetMuzzlePosition();
        Vector2 direction = GetEmissionDirection();
        for (int i = 0; i < mouthCount; i++)
        {
            float progress = (float)i / MouthPointCount;
            Vector2 position = muzzlePosition + direction * (_width * progress * 0.8f);
            float mouthRadius = radius * Mathf.Lerp(_mouthWidthRatio, 1f, progress);
            _surfacePoints[sampleCount + i] = new Vector4(position.x, position.y, mouthRadius, 0f);
        }
        int count = sampleCount + mouthCount;
        var minimum = new Vector2(float.MaxValue, float.MaxValue);
        var maximum = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < count; i++)
        {
            Vector4 point = _surfacePoints[i];
            Vector2 extent = Vector2.one * point.z;
            var center = new Vector2(point.x, point.y);
            minimum = Vector2.Min(minimum, center - extent);
            maximum = Vector2.Max(maximum, center + extent);
        }
        _quadVertices[0] = transform.InverseTransformPoint(new Vector3(minimum.x, minimum.y));
        _quadVertices[1] = transform.InverseTransformPoint(new Vector3(maximum.x, minimum.y));
        _quadVertices[2] = transform.InverseTransformPoint(new Vector3(maximum.x, maximum.y));
        _quadVertices[3] = transform.InverseTransformPoint(new Vector3(minimum.x, maximum.y));
        _mesh.vertices = _quadVertices;
        _mesh.RecalculateBounds();
        _runtimeMaterial.SetVectorArray(MetaballsId, _surfacePoints);
        _runtimeMaterial.SetInt(PointCountId, count);
        _runtimeMaterial.SetVector(FlowDirectionId, new Vector4(direction.x, direction.y, 0f, 0f));
        _renderer.enabled = true;
    }

    private void OnDisable()
    {
        ResetStream();
    }

    private void OnDestroy()
    {
        if (_mesh != null)
            Destroy(_mesh);
        if (_runtimeMaterial != null)
            Destroy(_runtimeMaterial);
    }
}
