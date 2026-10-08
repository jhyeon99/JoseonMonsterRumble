using TMPro;
using UnityEngine;

public class UI_PotionCount : MonoBehaviour
{
    [SerializeField] private PlayerHealth _playerHealth;
    [SerializeField] private TMP_Text _countText;

    private void Awake()
    {
        // 비어 있으면 씬에서 자동으로 찾음 (UI_HealthBar와 같은 방식)
        if (_playerHealth == null)
        {
            _playerHealth = FindFirstObjectByType<PlayerHealth>();
        }

        if (_playerHealth == null)
        {
            Debug.LogWarning($"{name}: PlayerHealth를 찾지 못했습니다.", this);
        }
    }

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