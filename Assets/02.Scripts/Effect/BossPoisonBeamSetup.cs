using UnityEngine;

/// <summary>보스와 독립된 VFX 프리팹의 파티클을 한 번 구성한다.</summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class BossPoisonBeamSetup : MonoBehaviour
{
    [SerializeField] private BossPoisonBeam _beam;
    [SerializeField] private Material _particleMaterial;
    [SerializeField] private Material _impactCloudMaterial;
    [SerializeField] private Material _mouthMaterial;
    [SerializeField] private Material _residualMaterial;

    private void Awake()
    {
        if (!BossPoisonParticleFactory.InitializeParticles(_beam, _particleMaterial, _impactCloudMaterial,
            _mouthMaterial, _residualMaterial))
        {
            enabled = false;
        }
    }
}
