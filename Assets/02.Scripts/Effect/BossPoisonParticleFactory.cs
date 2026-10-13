using UnityEngine;

/// <summary>프리뷰와 실제 패턴에서 같은 독분사 파티클 설정을 사용한다.</summary>
public static class BossPoisonParticleFactory
{
    public static bool InitializeParticles(BossPoisonBeam beam, Material particleMaterial, Material impactCloudMaterial)
    {
        if (beam == null || particleMaterial == null)
        {
            Debug.LogWarning("독분사: Beam 또는 파티클 Material 참조가 없습니다.", beam);
            return false;
        }
        beam.SetParticles(CreateBeamParticles(beam, particleMaterial, "MouthParticles", false),
            CreateBeamParticles(beam, particleMaterial, "HeadParticles", true),
            CreateImpactCloudParticles(beam, particleMaterial, impactCloudMaterial),
            CreateResidualParticles(beam, particleMaterial));
        return true;
    }

    private static ParticleSystem CreateResidualParticles(BossPoisonBeam beam, Material particleMaterial)
    {
        ParticleSystem particles = CreateBeamParticles(beam, particleMaterial, "ResidualDroplets", true);
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = false;
        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.35f, 0.8f), new Keyframe(1f, 0.05f)));
        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
        return particles;
    }

    private static ParticleSystem CreateImpactCloudParticles(BossPoisonBeam beam, Material particleMaterial, Material impactCloudMaterial)
    {
        ParticleSystem particles = CreateBeamParticles(beam, particleMaterial, "ImpactCloudParticles", true);
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
        if (impactCloudMaterial != null)
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = impactCloudMaterial;
        else
            Debug.LogWarning("독분사 테스트: 충돌 덩어리 Material이 없어 방울 Material을 사용합니다.", beam);
        return particles;
    }

    private static ParticleSystem CreateBeamParticles(BossPoisonBeam beam, Material particleMaterial, string name, bool isHead)
    {
        var particleObject = new GameObject(name);
        particleObject.transform.SetParent(beam.transform, false);
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
        particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = particleMaterial;
        return particles;
    }

}
