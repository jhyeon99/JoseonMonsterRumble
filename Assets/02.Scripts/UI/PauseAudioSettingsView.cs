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
    [SerializeField] private Image _bgmMuteIcon;
    [SerializeField] private Image _sfxMuteIcon;
    [SerializeField] private Sprite _soundOnSprite;
    [SerializeField] private Sprite _soundOffSprite;
    [SerializeField] private Image _bgmFillImage;
    [SerializeField] private Image _sfxFillImage;

    [Header("닫기 연동")]
    [SerializeField] private Button _outsideDimmerButton;
    [SerializeField] private Button _closeButton;
    [SerializeField] private UnityEvent _onCloseRequested = new UnityEvent();

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

    public UnityEvent OnCloseRequested => _onCloseRequested;

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

        if (_outsideDimmerButton != null)
        {
            _outsideDimmerButton.onClick.AddListener(RequestClose);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(RequestClose);
        }

        BindAudioManager();
        RefreshView();
    }

    private void OnEnable()
    {
        if (_isReady)
        {
            SyncFromAudioManager();
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

        if (_outsideDimmerButton != null)
        {
            _outsideDimmerButton.onClick.RemoveListener(RequestClose);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(RequestClose);
        }

        UnbindAudioManager();
    }

    public void ApplySettings(float bgmVolume, float sfxVolume, bool isBgmMuted, bool isSfxMuted)
    {
        _bgmVolume = Mathf.Clamp01(bgmVolume);
        _sfxVolume = Mathf.Clamp01(sfxVolume);
        _isBgmMuted = isBgmMuted;
        _isSfxMuted = isSfxMuted;

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

    public void RequestClose()
    {
        _onCloseRequested.Invoke();
    }

    private void HandleBgmVolumeChanged(float value)
    {
        _bgmVolume = Mathf.Clamp01(value);
        _bgmVolumeChanged.Invoke(_bgmVolume);
        if (_isReady && _boundAudioManager != null)
        {
            _boundAudioManager.PlaySlider();
        }
    }

    private void HandleSfxVolumeChanged(float value)
    {
        _sfxVolume = Mathf.Clamp01(value);
        _sfxVolumeChanged.Invoke(_sfxVolume);
        if (_isReady && _boundAudioManager != null)
        {
            _boundAudioManager.PlaySlider();
        }
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
        if (_bgmMuteStateText != null)
        {
            _bgmMuteStateText.text = _isBgmMuted ? "MUTE ON" : "MUTE OFF";
            _bgmMuteStateText.color = _isBgmMuted ? _mutedColor : _activeColor;
        }

        if (_bgmMuteIcon != null)
        {
            if (_isBgmMuted && _soundOffSprite != null)
            {
                _bgmMuteIcon.sprite = _soundOffSprite;
            }
            else if (!_isBgmMuted && _soundOnSprite != null)
            {
                _bgmMuteIcon.sprite = _soundOnSprite;
            }
        }

        if (_bgmFillImage != null)
        {
            _bgmFillImage.color = _isBgmMuted ? _mutedColor : _activeColor;
        }
    }

    private void RefreshSfxView()
    {
        if (_sfxMuteStateText != null)
        {
            _sfxMuteStateText.text = _isSfxMuted ? "MUTE ON" : "MUTE OFF";
            _sfxMuteStateText.color = _isSfxMuted ? _mutedColor : _activeColor;
        }

        if (_sfxMuteIcon != null)
        {
            if (_isSfxMuted && _soundOffSprite != null)
            {
                _sfxMuteIcon.sprite = _soundOffSprite;
            }
            else if (!_isSfxMuted && _soundOnSprite != null)
            {
                _sfxMuteIcon.sprite = _soundOnSprite;
            }
        }

        if (_sfxFillImage != null)
        {
            _sfxFillImage.color = _isSfxMuted ? _mutedColor : _activeColor;
        }
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

        if (_bgmMuteStateText == null && _bgmMuteIcon == null)
        {
            Debug.LogWarning($"{name}: BGM 음소거 표시를 위한 Text 또는 Icon 참조가 없습니다.", this);
            hasRequiredReferences = false;
        }

        if (_sfxMuteStateText == null && _sfxMuteIcon == null)
        {
            Debug.LogWarning($"{name}: SFX 음소거 표시를 위한 Text 또는 Icon 참조가 없습니다.", this);
            hasRequiredReferences = false;
        }

        return hasRequiredReferences;
    }

    private AudioManager _boundAudioManager;

    private void BindAudioManager()
    {
        UnbindAudioManager();
        _boundAudioManager = AudioManager.Instance;
        if (_boundAudioManager == null)
            return;

        _bgmVolumeChanged.AddListener(_boundAudioManager.SetBgmVolume);
        _sfxVolumeChanged.AddListener(_boundAudioManager.SetSfxVolume);
        _bgmMuteChanged.AddListener(_boundAudioManager.SetBgmMute);
        _sfxMuteChanged.AddListener(_boundAudioManager.SetSfxMute);
        SyncFromAudioManager();
    }

    private void UnbindAudioManager()
    {
        // Delegate removal needs the original managed reference, even after Unity destroys it.
        if (ReferenceEquals(_boundAudioManager, null))
            return;

        _bgmVolumeChanged.RemoveListener(_boundAudioManager.SetBgmVolume);
        _sfxVolumeChanged.RemoveListener(_boundAudioManager.SetSfxVolume);
        _bgmMuteChanged.RemoveListener(_boundAudioManager.SetBgmMute);
        _sfxMuteChanged.RemoveListener(_boundAudioManager.SetSfxMute);
        _boundAudioManager = null;
    }

    private void SyncFromAudioManager()
    {
        if (_boundAudioManager == null)
            return;

        _bgmVolume = _boundAudioManager.GetBgmVolume();
        _sfxVolume = _boundAudioManager.GetSfxVolume();
        _isBgmMuted = _boundAudioManager.IsBgmMuted();
        _isSfxMuted = _boundAudioManager.IsSfxMuted();
    }
}
