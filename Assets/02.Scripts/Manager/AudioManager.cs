using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class AudioManager : MonoBehaviour, GameAudio.IAudioEventPlayer
{
    private const string PrefKeyBgmVolume = "Audio_BgmVolume";
    private const string PrefKeySfxVolume = "Audio_SfxVolume";
    private const string PrefKeyBgmMute = "Audio_BgmMuted";
    private const string PrefKeySfxMute = "Audio_SfxMuted";
    [SerializeField, Min(1)] private int _sfxChannelCount = 10;
    private const float DefaultVolume = 1f;

    private static AudioManager _instance;

    [Header("BGM 채널")]
    [SerializeField] private AudioSource _bgmSource;

    [Header("UI 오디오 데이터")]
    [SerializeField] private UIAudioDataSO _uiAudioData;

    [Header("상태 모니터링")]
    [SerializeField, ReadOnly] private float _bgmVolume = DefaultVolume;
    [SerializeField, ReadOnly] private float _sfxVolume = DefaultVolume;
    [SerializeField, ReadOnly] private bool _isBgmMuted;
    [SerializeField, ReadOnly] private bool _isSfxMuted;

    private readonly List<AudioSource> _sfxPool = new List<AudioSource>(10);
    private Coroutine _bgmFadeCoroutine;

    public static AudioManager Instance
    {
        get
        {
            if (_instance == null)
            {
                AudioManager existing = FindFirstObjectByType<AudioManager>();
                if (existing != null)
                {
                    _instance = existing;
                }
                else
                {
                    var managerObject = new GameObject("AudioManager");
                    _instance = managerObject.AddComponent<AudioManager>();
                }
            }

            return _instance;
        }
    }

    public UIAudioDataSO UIAudioData => _uiAudioData;
    public AudioClip ButtonHoverClip => _uiAudioData != null ? _uiAudioData.ButtonHoverClip : null;
    public AudioClip ButtonClickClip => _uiAudioData != null ? _uiAudioData.ButtonClickClip : null;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        InitializeAudioSources();
        LoadSettings();
        InitializeRuntime();
    }

    public float GetBgmVolume()
    {
        return _bgmVolume;
    }

    public float GetSfxVolume()
    {
        return _sfxVolume;
    }

    public bool IsBgmMuted()
    {
        return _isBgmMuted;
    }

    public bool IsSfxMuted()
    {
        return _isSfxMuted;
    }

    public void SetBgmVolume(float volume)
    {
        _bgmVolume = Mathf.Clamp01(volume);
        ApplyBgmVolume();
        PlayerPrefs.SetFloat(PrefKeyBgmVolume, _bgmVolume);
        PlayerPrefs.Save();
    }

    public void SetSfxVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);
        ApplySfxVolume();
        PlayerPrefs.SetFloat(PrefKeySfxVolume, _sfxVolume);
        PlayerPrefs.Save();
    }

    public void SetBgmMute(bool isMuted)
    {
        _isBgmMuted = isMuted;
        ApplyBgmVolume();
        PlayerPrefs.SetInt(PrefKeyBgmMute, _isBgmMuted ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetSfxMute(bool isMuted)
    {
        _isSfxMuted = isMuted;
        ApplySfxVolume();
        PlayerPrefs.SetInt(PrefKeySfxMute, _isSfxMuted ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void PlayBgm(AudioClip clip, float fadeDuration = 0.5f)
    {
        if (clip != null) RequestLegacyBgm(clip, fadeDuration);
    }

    public void StopBgm(float fadeDuration = 0.5f)
    {
        BeginMusicStop(Mathf.Max(0f, fadeDuration));
    }

    public void PlaySfx(AudioClip clip, float volumeScale = 1f, float gain = 1f)
    {
        PlaySfxTracked(clip, volumeScale, gain);
    }

    public AudioSource PlaySfxTracked(AudioClip clip, float volumeScale = 1f, float gain = 1f)
    {
        if (clip == null || _isSfxMuted) return null;
        return PlayLegacySfx(clip, Mathf.Clamp01(volumeScale) * Mathf.Clamp(gain, 0f, 3f));
    }

    public void PlayButtonHover()
    {
        if (_uiAudioData != null && _uiAudioData.ButtonHoverClip != null)
        {
            PlaySfx(_uiAudioData.ButtonHoverClip, _uiAudioData.ButtonHoverVolume);
        }
    }

    public void PlayButtonClick()
    {
        if (_uiAudioData != null && _uiAudioData.ButtonClickClip != null)
        {
            PlaySfx(_uiAudioData.ButtonClickClip, _uiAudioData.ButtonClickVolume);
        }
    }

    public void PlayPanelOpen()
    {
        if (_uiAudioData != null && _uiAudioData.PanelOpenClip != null)
        {
            PlaySfx(_uiAudioData.PanelOpenClip, _uiAudioData.PanelOpenVolume);
        }
    }

    public void PlayPanelClose()
    {
        if (_uiAudioData != null && _uiAudioData.PanelCloseClip != null)
        {
            PlaySfx(_uiAudioData.PanelCloseClip, _uiAudioData.PanelCloseVolume);
        }
    }

    private void InitializeAudioSources()
    {
        if (_bgmSource == null)
        {
            var bgmObject = new GameObject("BgmChannel");
            bgmObject.transform.SetParent(transform);
            _bgmSource = bgmObject.AddComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.playOnAwake = false;
            _bgmSource.spatialBlend = 0f;
        }

        if (_sfxPool.Count == 0)
        {
            var sfxRoot = new GameObject("SfxPool");
            sfxRoot.transform.SetParent(transform);

            for (int i = 0; i < Mathf.Max(1, _sfxChannelCount); i++)
            {
                var sfxObject = new GameObject($"SfxChannel_{i:D2}");
                sfxObject.transform.SetParent(sfxRoot.transform);
                AudioSource source = sfxObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.volume = 1f;
                source.mute = false;
                source.bypassEffects = true;
                source.bypassListenerEffects = true;
                source.bypassReverbZones = true;
                _sfxPool.Add(source);
            }
        }
    }

    private void LoadSettings()
    {
        _bgmVolume = PlayerPrefs.GetFloat(PrefKeyBgmVolume, DefaultVolume);
        _sfxVolume = PlayerPrefs.GetFloat(PrefKeySfxVolume, DefaultVolume);
        _isBgmMuted = PlayerPrefs.GetInt(PrefKeyBgmMute, 0) == 1;
        _isSfxMuted = PlayerPrefs.GetInt(PrefKeySfxMute, 0) == 1;

        ApplyBgmVolume();
        ApplySfxVolume();
    }

    private void ApplyBgmVolume()
    {
        if (_bgmSource != null)
            _bgmSource.volume = (_isBgmMuted ? 0f : _bgmVolume) * _musicVolume * _musicFade;
    }

    private void ApplySfxVolume()
    {
        foreach (AudioSource source in _sfxPool)
        {
            float gain = 1f;
            if (_channels.TryGetValue(source, out Request request)) gain = request.Volume;
            source.volume = (_isSfxMuted ? 0f : _sfxVolume) * gain;
        }
    }
}