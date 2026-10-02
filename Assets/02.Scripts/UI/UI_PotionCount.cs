using TMPro;
using UnityEngine;

public class UI_PotionCount : MonoBehaviour
{
    [SerializeField] private PlayerHealth _playerHealth;
    [SerializeField] private TMP_Text _countText;

    private void OnEnable()
    {
        if (_playerHealth != null)
        {
            _playerHealth.OnPotionChanged += UpdateText;
        }
    }

    private void OnDisable()
    {
        if (_playerHealth != null)
        {
            _playerHealth.OnPotionChanged -= UpdateText;
        }
    }

    private void Start()
    {
        if (_playerHealth != null)
        {
            UpdateText(_playerHealth.PotionCount);
        }
    }

    private void UpdateText(int count)
    {
        if (_countText != null)
        {
            _countText.text = count.ToString();
        }
    }
}