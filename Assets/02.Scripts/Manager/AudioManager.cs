using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    private const string PrefKeyBgmVolume = "Audio_BgmVolume";
    private const string PrefKeySfxVolume = "Audio_SfxVolume";
    private const string PrefKeyBgmMute = "Audio_BgmMuted";
    private const string PrefKeySfxMute = "Audio_SfxMuted";
    private const int SfxChannelCount = 10;
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

    private readonly List<AudioSource> _sfxPool = new List<AudioSource>(SfxChannelCount);
    private int _sfxPoolIndex;
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
        if (clip == null)
        {
            return;
        }

        if (_bgmSource.clip == clip && _bgmSource.isPlaying)
        {
            return;
        }

        if (_bgmFadeCoroutine != null)
        {
            StopCoroutine(_bgmFadeCoroutine);
        }

        _bgmFadeCoroutine = StartCoroutine(FadeBgmRoutine(clip, fadeDuration));
    }

    public void StopBgm(float fadeDuration = 0.5f)
    {
        if (!_bgmSource.isPlaying)
        {
            return;
        }

        if (_bgmFadeCoroutine != null)
        {
            StopCoroutine(_bgmFadeCoroutine);
        }

        _bgmFadeCoroutine = StartCoroutine(FadeBgmRoutine(null, fadeDuration));
    }

    public void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || _isSfxMuted)
        {
            return;
        }

        AudioSource source = GetNextSfxSource();
        if (source == null)
        {
            return;
        }

        float finalVolume = Mathf.Clamp01(volumeScale) * _sfxVolume;
        source.PlayOneShot(clip, finalVolume);
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

            for (int i = 0; i < SfxChannelCount; i++)
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
        {
            _bgmSource.volume = _isBgmMuted ? 0f : _bgmVolume;
        }
    }

    private void ApplySfxVolume()
    {
        for (int i = 0; i < _sfxPool.Count; i++)
        {
            if (_sfxPool[i] != null)
            {
                _sfxPool[i].volume = _isSfxMuted ? 0f : 1f;
            }
        }
    }

    private AudioSource GetNextSfxSource()
    {
        if (_sfxPool.Count == 0)
        {
            return null;
        }

        for (int i = 0; i < _sfxPool.Count; i++)
        {
            int checkIndex = (_sfxPoolIndex + i) % _sfxPool.Count;
            if (!_sfxPool[checkIndex].isPlaying)
            {
                _sfxPoolIndex = (checkIndex + 1) % _sfxPool.Count;
                return _sfxPool[checkIndex];
            }
        }

        AudioSource fallbackSource = _sfxPool[_sfxPoolIndex];
        _sfxPoolIndex = (_sfxPoolIndex + 1) % _sfxPool.Count;
        return fallbackSource;
    }

    private IEnumerator FadeBgmRoutine(AudioClip newClip, float duration)
    {
        float startVolume = _bgmSource.volume;
        float targetVolume = _isBgmMuted ? 0f : _bgmVolume;

        if (duration > 0f && _bgmSource.isPlaying)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
                yield return null;
            }
        }

        _bgmSource.Stop();
        _bgmSource.clip = newClip;

        if (newClip != null)
        {
            _bgmSource.Play();
            if (duration > 0f)
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _bgmSource.volume = Mathf.Lerp(0f, targetVolume, elapsed / duration);
                    yield return null;
                }
            }
            _bgmSource.volume = targetVolume;
        }

        _bgmFadeCoroutine = null;
    }
}
