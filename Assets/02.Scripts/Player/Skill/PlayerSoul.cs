using System;
using UnityEngine;
using UnityEngine.Events;

public class PlayerSoul : MonoBehaviour, ISoulSource
{
    [SerializeField] private float _maxSoul = 5f;
    public float MaxSoul => _maxSoul;
    [SerializeField, ReadOnly] private float _currentSoul = 0f;
    public float CurrentSoul => _currentSoul;

    public event Action<float, float> OnSoulChanged;

    public void AddSoul(float amount)
    {
        _currentSoul = Mathf.Min(_currentSoul + amount, _maxSoul);
        OnSoulChanged?.Invoke(_currentSoul, _maxSoul);
    }
}