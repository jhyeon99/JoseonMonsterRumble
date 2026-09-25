using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class PauseAudioSettingsView : MonoBehaviour
{
    [SerializeField] private Slider _bgmSlider;
    [SerializeField] private Slider _sfxSlider;
    [SerializeField] private Button _bgmMuteButton;
    [SerializeField] private Button _sfxMuteButton;
    [SerializeField] private TextMeshProUGUI _bgmMuteStateText;
    [SerializeField] private TextMeshProUGUI _sfxMuteStateText;
    [SerializeField] private Image _bgmFillImage;
    [SerializeField] private Image _sfxFillImage;

    [Header("초기 설정")]
    [SerializeField, Range(0f, 1f)] private float _bgmVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float _sfxVolume = 1f;
    [SerializeField] private bool _isBgmMuted;
    [SerializeField] private bool _isSfxMuted;

    [Header("상태 색상")]
    [SerializeField] private Color _activeColor = new Color(0.92f, 0.78f, 0.42f, 1f);
    [SerializeField] private Color _mutedColor = new Color(0.42f, 0.42f, 0.45f, 0.8f);

    [Header("AudioManager 연결 이벤트")]
    [SerializeField] private UnityEvent<float> _bgmVolumeChanged = new UnityEvent<float>();
    [SerializeField] private UnityEvent<float> _sfxVolumeChanged = new UnityEvent<float>();
    [SerializeField] private UnityEvent<bool> _bgmMuteChanged = new UnityEvent<bool>();
    [SerializeField] private UnityEvent<bool> _sfxMuteChanged = new UnityEvent<bool>();

    [SerializeField, ReadOnly] private bool _isReady;

    private void Awake()
    {
        _isReady = HasRequiredReferences();
        if (!_isReady)
        {
            enabled = false;
            return;
        }

        _bgmVolume = Mathf.Clamp01(_bgmVolume);
        _sfxVolume = Mathf.Clamp01(_sfxVolume);
        _bgmSlider.onValueChanged.AddListener(HandleBgmVolumeChanged);
        _sfxSlider.onValueChanged.AddListener(HandleSfxVolumeChanged);
        RefreshView();
    }

    private void OnEnable()
    {
        if (_isReady)
        {
            RefreshView();
        }
    }

    private void OnDestroy()
    {
        if (_bgmSlider != null)
        {
            _bgmSlider.onValueChanged.RemoveListener(HandleBgmVolumeChanged);
        }

        if (_sfxSlider != null)
        {
            _sfxSlider.onValueChanged.RemoveListener(HandleSfxVolumeChanged);
        }
    }

    public void ApplySettings(float bgmVolume, float sfxVolume, bool isBgmMuted, bool isSfxMuted)
    {
        _bgmVolume = Mathf.Clamp01(bgmVolume);
        _sfxVolume = Mathf.Clamp01(sfxVolume);
        _isBgmMuted = isBgmMuted;
        _isSfxMuted = isSfxMuted;

        // TODO: AudioManager 또는 설정 저장 시스템이 확정되면 PlayerPrefs 저장과 로드는 해당 계층에서 처리한다.
        if (_isReady)
        {
            RefreshView();
        }
    }

    public void ToggleBgmMute()
    {
        if (!_isReady)
        {
            return;
        }

        _isBgmMuted = !_isBgmMuted;
        RefreshBgmView();
        _bgmMuteChanged.Invoke(_isBgmMuted);
    }

    public void ToggleSfxMute()
    {
        if (!_isReady)
        {
            return;
        }

        _isSfxMuted = !_isSfxMuted;
        RefreshSfxView();
        _sfxMuteChanged.Invoke(_isSfxMuted);
    }

    private void HandleBgmVolumeChanged(float value)
    {
        _bgmVolume = Mathf.Clamp01(value);
        _bgmVolumeChanged.Invoke(_bgmVolume);
    }

    private void HandleSfxVolumeChanged(float value)
    {
        _sfxVolume = Mathf.Clamp01(value);
        _sfxVolumeChanged.Invoke(_sfxVolume);
    }

    private void RefreshView()
    {
        _bgmSlider.SetValueWithoutNotify(_bgmVolume);
        _sfxSlider.SetValueWithoutNotify(_sfxVolume);
        RefreshBgmView();
        RefreshSfxView();
    }

    private void RefreshBgmView()
    {
        _bgmMuteStateText.text = _isBgmMuted ? "MUTE ON" : "MUTE OFF";
        _bgmMuteStateText.color = _isBgmMuted ? _mutedColor : _activeColor;
        _bgmFillImage.color = _isBgmMuted ? _mutedColor : _activeColor;
    }

    private void RefreshSfxView()
    {
        _sfxMuteStateText.text = _isSfxMuted ? "MUTE ON" : "MUTE OFF";
        _sfxMuteStateText.color = _isSfxMuted ? _mutedColor : _activeColor;
        _sfxFillImage.color = _isSfxMuted ? _mutedColor : _activeColor;
    }

    private bool HasRequiredReferences()
    {
        bool hasRequiredReferences = true;

        if (_bgmSlider == null || _sfxSlider == null)
        {
            Debug.LogWarning($"{name}: BGM 또는 SFX Slider 참조가 없습니다.", this);
            hasRequiredReferences = false;
        }

        if (_bgmMuteButton == null || _sfxMuteButton == null)
        {
            Debug.LogWarning($"{name}: BGM 또는 SFX Mute Button 참조가 없습니다.", this);
            hasRequiredReferences = false;
        }

        if (_bgmMuteStateText == null || _sfxMuteStateText == null)
        {
            Debug.LogWarning($"{name}: BGM 또는 SFX Mute State Text 참조가 없습니다.", this);
            hasRequiredReferences = false;
        }

        if (_bgmFillImage == null || _sfxFillImage == null)
        {
            Debug.LogWarning($"{name}: BGM 또는 SFX Slider Fill Image 참조가 없습니다.", this);
            hasRequiredReferences = false;
        }

        return hasRequiredReferences;
    }
}
