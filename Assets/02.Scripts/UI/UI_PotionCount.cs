using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_PotionCount : MonoBehaviour
{
    [SerializeField] private PlayerHealth _playerHealth;
    [SerializeField] private TMP_Text _countText;
    [SerializeField] private Image _cooldownOverlay;

    private void Awake()
    {
        // 비어 있으면 씬에서 자동으로 찾음 (UI_HealthBar와 같은 방식)
        if (_playerHealth == null)
        {
            _playerHealth = FindFirstObjectByType<PlayerHealth>();
        }

        if (_cooldownOverlay == null)
        {
            Transform overlayTransform = transform.parent != null
                ? transform.parent.Find("Cooldown_Overlay")
                : null;

            if (overlayTransform != null)
            {
                _cooldownOverlay = overlayTransform.GetComponent<Image>();
            }
        }

        if (_playerHealth == null)
        {
            Debug.LogWarning($"{name}: PlayerHealth를 찾지 못했습니다.", this);
        }

        ResetCooldownOverlay();
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

        ResetCooldownOverlay();
    }

    private void Start()
    {
        if (_playerHealth != null)
        {
            UpdateText(_playerHealth.PotionCount);
        }
    }

    private void Update()
    {
        UpdateCooldown();
    }

    private void UpdateText(int count)
    {
        if (_countText != null)
        {
            _countText.text = count.ToString();
        }
    }

    private void UpdateCooldown()
    {
        if (_cooldownOverlay == null || _playerHealth == null) return;

        float ratio = _playerHealth.PotionCooldownRatio;
        if (ratio > 0f)
        {
            if (!_cooldownOverlay.gameObject.activeSelf)
            {
                _cooldownOverlay.gameObject.SetActive(true);
            }
            _cooldownOverlay.fillAmount = ratio;
        }
        else
        {
            if (_cooldownOverlay.gameObject.activeSelf)
            {
                _cooldownOverlay.fillAmount = 0f;
                _cooldownOverlay.gameObject.SetActive(false);
            }
        }
    }

    private void ResetCooldownOverlay()
    {
        if (_cooldownOverlay != null)
        {
            _cooldownOverlay.fillAmount = 0f;
            _cooldownOverlay.gameObject.SetActive(false);
        }
    }
}