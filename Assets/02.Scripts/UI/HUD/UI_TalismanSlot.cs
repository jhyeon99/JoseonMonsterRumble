using UnityEngine;
using UnityEngine.UI;

public class UI_TalismanSlot : MonoBehaviour
{
    [Header("슬롯 컴포넌트 참조")]
    [SerializeField] private Image _frameImage;
    [SerializeField] private Image _bgImage;
    [SerializeField] private Image _iconImage;
    [SerializeField] private Image _cooldownOverlay;

    [Header("슬롯 배경 색상 설정")]
    [SerializeField] private Color _equippedBgColor = new Color(0.824f, 0.757f, 0.620f, 1f);
    [SerializeField] private Color _emptyBgColor = new Color(0.271f, 0.251f, 0.231f, 1f);
    [SerializeField] private Color _frameColor = new Color(0.141f, 0.125f, 0.114f, 1f);

    public void SetTalisman(Sprite talismanSprite)
    {
        if (_frameImage != null)
        {
            _frameImage.color = _frameColor;
        }

        if (_bgImage != null)
        {
            _bgImage.color = _equippedBgColor;
        }

        if (_iconImage != null)
        {
            _iconImage.gameObject.SetActive(true);
            _iconImage.sprite = talismanSprite;
        }

        if (_cooldownOverlay != null)
        {
            _cooldownOverlay.fillAmount = 0f;
        }
    }

    public void SetEmpty()
    {
        if (_frameImage != null)
        {
            _frameImage.color = _frameColor;
        }

        if (_bgImage != null)
        {
            _bgImage.color = _emptyBgColor;
        }

        if (_iconImage != null)
        {
            _iconImage.gameObject.SetActive(false);
        }

        if (_cooldownOverlay != null)
        {
            _cooldownOverlay.fillAmount = 0f;
        }
    }

    public void UpdateCooldown(float normalizedCooldown)
    {
        if (_cooldownOverlay != null)
        {
            _cooldownOverlay.fillAmount = Mathf.Clamp01(normalizedCooldown);
        }
    }
}
