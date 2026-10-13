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

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            AddSoul(1f);
        }
    }

    public void AddSoul(float amount)
    {
        _currentSoul = Mathf.Min(_currentSoul + amount, _maxSoul);
        OnSoulChanged?.Invoke(_currentSoul, _maxSoul);
    }

    public bool TryUseSoul(float amount)
    {
        if (_currentSoul < amount) return false;

        _currentSoul -= amount;
        OnSoulChanged?.Invoke(_currentSoul, _maxSoul);
        return true;
    }
}