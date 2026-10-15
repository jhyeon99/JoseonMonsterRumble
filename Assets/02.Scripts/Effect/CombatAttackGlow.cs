using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatAttackGlow : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _renderer;
    [SerializeField] private Sprite[] _attackSprites;
    [SerializeField, Range(0f, 8f)] private float _emissionStrength = 0.6f;
    private static readonly int EmissionStrength = Shader.PropertyToID("_EmissionStrength");
    private HashSet<Sprite> _sprites;
    private MaterialPropertyBlock _properties;

    private void Awake()
    {
        if (_renderer == null || _attackSprites == null || _attackSprites.Length == 0)
        {
            Debug.LogWarning("CombatAttackGlow: 플레이어 렌더러와 공격 Sprite 연결이 필요합니다.", this);
            enabled = false;
            return;
        }
        _sprites = new HashSet<Sprite>(_attackSprites);
        _properties = new MaterialPropertyBlock();
    }

    private void LateUpdate()
    {
        _renderer.GetPropertyBlock(_properties);
        float strength = _sprites.Contains(_renderer.sprite) ? _emissionStrength : 0f;
        if (_properties.GetFloat(EmissionStrength) == strength) return;
        _properties.SetFloat(EmissionStrength, strength);
        _renderer.SetPropertyBlock(_properties);
    }

    private void OnDisable()
    {
        if (_renderer == null || _properties == null) return;
        _renderer.GetPropertyBlock(_properties);
        _properties.SetFloat(EmissionStrength, 0f);
        _renderer.SetPropertyBlock(_properties);
    }
}
