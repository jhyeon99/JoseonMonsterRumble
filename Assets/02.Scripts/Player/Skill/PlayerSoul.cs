using UnityEngine;

public class PlayerSoul : MonoBehaviour
{
    [SerializeField] private float _maxSoul = 5f;
    [SerializeField, ReadOnly] private float _currentSoul = 0f;
    public float CurrentSoul => _currentSoul;

    public void AddSoul(float amount)
    {
        _currentSoul = Mathf.Min(_currentSoul + amount, _maxSoul);
    }
}