using System;
using System.Collections;
using System.Collections.Generic;
using GameAudio;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour, GameAudio.IAudioEventPlayer
{
    private const string PrefKeyBgmVolume = "Audio_BgmVolume";
    private const string PrefKeySfxVolume = "Audio_SfxVolume";
    private const string PrefKeyBgmMute = "Audio_BgmMuted";
    private const string PrefKeySfxMute = "Audio_SfxMuted";
    [SerializeField, Min(1)] private int _sfxChannelCount = 10;
    private const float DefaultVolume = 1f;

    private static AudioManager _instance;
    private static bool _isShuttingDown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetLifecycle()
    {
        _instance = null;
        _isShuttingDown = false;
    }

    [Header("BGM 채널")]
    [SerializeField] private AudioSource _bgmSource;

    [Header("패링 / 패링 3타 BGM 덕킹")]
    [UnityEngine.Serialization.FormerlySerializedAs("_enableFinisherBgmDucking")]
    [SerializeField] private bool _enableCombatBgmDucking = true;
    [SerializeField, Range(0f, 1f)] private float _parryDuckedVolume = 0.5f;
    [SerializeField, Min(0.01f)] private float _parryDuckRecoveryTime = 0.25f;
    [Tooltip("타격 전 진행도가 이 값에 도달하면 BGM을 낮추기 시작합니다. 1은 타격 순간입니다.")]
    [SerializeField, Range(0f, 0.99f)] private float _finisherDuckStartProgress = 0.2f;
    [SerializeField, Range(0f, 1f)] private float _finisherDuckedVolume = 0.2f;
    [SerializeField, Min(0f)] private float _finisherDuckHoldTime = 0.06f;
    [SerializeField, Min(0.01f)] private float _finisherDuckRecoveryTime = 0.55f;

    private float _finisherBgmGain = 1f;
    private float _finisherDuckElapsed;
    private float _finisherRecoveryStartGain;
    private bool _isFinisherDuckRecovering;
    private bool _isFinisherAudioPreparing;
    private float _finisherAnticipationStartGain;
    private float _activeDuckRecoveryTime;

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
            if (_isShuttingDown || !Application.isPlaying)
                return null;
            if (_instance == null)
            {
                AudioManager existing = FindFirstObjectByType<AudioManager>();
                if (existing != null)
                {
                    _instance = existing;
                }
                else
                {
                    AudioManagerPrefabSO reference = Resources.Load<AudioManagerPrefabSO>("Audio/SO_AudioManagerPrefab");
                    GameObject prefab = reference != null ? reference.Prefab : null;
                    if (prefab != null)
                    {
                        Instantiate(prefab);
                    }
                    else
                    {
                        var managerObject = new GameObject("AudioManager");
                        _instance = managerObject.AddComponent<AudioManager>();
                    }
                }
            }

            return _instance;
        }
    }

    public static AudioManager ExistingInstance => _instance;
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
        if (this == null || _instance != this || !isActiveAndEnabled)
            return;
        if (clip != null)
        {
            RequestLegacyBgm(clip, fadeDuration);
        }
    }

    public void StopBgm(float fadeDuration = 0.5f)
    {
        if (this == null || _instance != this || !isActiveAndEnabled)
            return;
        BeginMusicStop(Mathf.Max(0f, fadeDuration));
    }

    public void PlaySfx(AudioClip clip, float volumeScale = 1f, float gain = 1f)
    {
        PlaySfxTracked(clip, volumeScale, gain);
    }

    public AudioSource PlaySfxTracked(AudioClip clip, float volumeScale = 1f, float gain = 1f)
    {
        if (this == null || _instance != this || !isActiveAndEnabled)
            return null;
        if (clip == null || _isSfxMuted)
            return null;
        return PlayLegacySfx(clip, Mathf.Clamp01(volumeScale) * Mathf.Clamp(gain, 0f, 3f));
    }

    // Once assigned, the catalog is authoritative, including silent/disabled entries.
    public PlayHandle PlayUiEvent(string eventId, AudioClip legacyClip = null, float legacyVolume = 1f)
    {
        if (this == null || _instance != this || !isActiveAndEnabled)
            return PlayHandle.Empty;
        if (_catalog != null && _catalog.UI != null)
            return PlayEventCore(_catalog.UI, eventId, default, AudioTimeMode.RealTime, LifetimePolicy.Global, AudioOutputGroup.Sfx);
        if (!isActiveAndEnabled || legacyClip == null ||
            (eventId != "UI.Click" && eventId != "UI.Hover" && eventId != "UI.PanelOpen" &&
             eventId != "UI.PanelClose" && eventId != "UI.Slider"))
        {
            return PlayHandle.Empty;
        }

        var request = new Request
        {
            Clip = legacyClip,
            Volume = Mathf.Clamp01(legacyVolume),
            Pitch = 1f,
            SoundId = "legacy:" + legacyClip.GetInstanceID(),
            EventId = eventId,
            Priority = 70,
            MaxCount = 4,
            Lifetime = LifetimePolicy.Global,
            TimeMode = AudioTimeMode.RealTime
        };
        AddRequest(request);
        StartRequest(request);
        return IsHandleValid(request.Handle) ? request.Handle : PlayHandle.Empty;
    }

    public void PlayButtonHover() => PlayUiEvent("UI.Hover", ButtonHoverClip, _uiAudioData != null ? _uiAudioData.ButtonHoverVolume : 1f);
    public void PlayButtonClick() => PlayUiEvent("UI.Click", ButtonClickClip, _uiAudioData != null ? _uiAudioData.ButtonClickVolume : 1f);
    public void PlayPanelOpen() => PlayUiEvent("UI.PanelOpen", _uiAudioData != null ? _uiAudioData.PanelOpenClip : null, _uiAudioData != null ? _uiAudioData.PanelOpenVolume : 1f);
    public void PlayPanelClose() => PlayUiEvent("UI.PanelClose", _uiAudioData != null ? _uiAudioData.PanelCloseClip : null, _uiAudioData != null ? _uiAudioData.PanelCloseVolume : 1f);
    public void PlaySlider() => PlayUiEvent("UI.Slider", _uiAudioData != null ? _uiAudioData.SliderTickClip : null, _uiAudioData != null ? _uiAudioData.SliderTickVolume : 1f);

    public PlayHandle PlayEntityEvent(SoundSetSO set, string eventId, Component source, AudioOwnerKey owner, AudioClip excludedClip = null)
    {
        if (source == null || owner.IsEmpty) return PlayHandle.Empty;
        var context = new AudioContext(owner, 0, AudioLifetimeRegistry.ForScene(source.gameObject.scene), source.transform.position);
        return PlayEventCore(set, eventId, context, requiredLifetime: LifetimePolicy.Entity,
            requiredOutput: AudioOutputGroup.Sfx, excludedClip: excludedClip);
    }

    public PlayHandle PlayEntityLoop(SoundSetSO set, string eventId, Component source, AudioOwnerKey owner)
    {
        if (source == null || owner.IsEmpty) return PlayHandle.Empty;
        var context = new AudioContext(owner, 0, AudioLifetimeRegistry.ForScene(source.gameObject.scene), source.transform.position);
        return PlayEventCore(set, eventId, context, requiredLifetime: LifetimePolicy.Entity,
            requiredOutput: AudioOutputGroup.Sfx, dedicatedChannel: true);
    }

    public PlayHandle PlaySceneEvent(string eventId, Component source)
    {
        if (source == null || string.IsNullOrWhiteSpace(eventId) || _catalog == null)
            return PlayHandle.Empty;
        int separator = eventId.IndexOf('.');
        if (separator < 1) return PlayHandle.Empty;
        SoundSetSO set = _catalog.GetSet(eventId.Substring(0, separator));
        var context = new AudioContext(default, 0, AudioLifetimeRegistry.ForScene(source.gameObject.scene), source.transform.position);
        return PlayEvent(set, eventId, context);
    }

    public AudioClip GetEventClip(string eventId)
    {
        if (_catalog == null || string.IsNullOrWhiteSpace(eventId)) return null;
        int separator = eventId.IndexOf('.');
        SoundSetSO set = separator > 0 ? _catalog.GetSet(eventId.Substring(0, separator)) : null;
        if (set == null || !set.TryGetEvent(eventId, out EventBinding binding) || !binding.Enabled)
            return null;
        if (!_catalog.TryGetSound(binding.SoundId, out SoundData sound) || sound.Clips == null)
            return null;
        foreach (AudioClip clip in sound.Clips)
        {
            if (clip != null) return clip;
        }
        return null;
    }

    public PlayHandle PlayMusicEvent(string eventId, Scene scene)
    {
        if (this == null || _instance != this || !isActiveAndEnabled)
            return PlayHandle.Empty;
        if (!scene.IsValid() || !scene.isLoaded)
            return PlayHandle.Empty;
        return PlayEventCore(_catalog != null ? _catalog.Music : null, eventId,
            new AudioContext(default, 0, AudioLifetimeRegistry.ForScene(scene), Vector3.zero),
            AudioTimeMode.RealTime, LifetimePolicy.Scene, AudioOutputGroup.Bgm);
    }

    private AudioSceneKey _sceneMusicKey;
    private void OnEnable()
    {
        if (_instance != this)
            return;
        UnsubscribeSceneEvents();
        SceneManager.sceneLoaded += OnAudioSceneLoaded;
        SceneManager.activeSceneChanged += OnAudioActiveSceneChanged;
        GameEvents.OnFinisherAnticipation += HandleFinisherAudioAnticipation;
        GameEvents.OnFinisherImpact += HandleFinisherAudioImpact;
        GameEvents.OnFinisherAnticipationCancelled += HandleFinisherAudioCancelled;
    }

    private void Start()
    {
        if (_instance == this && _currentMusic == null && _pendingMusic == null)
        {
            RequestSceneMusic(SceneManager.GetActiveScene());
        }
    }

    private void OnAudioSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single || scene == SceneManager.GetActiveScene())
        {
            RequestSceneMusic(scene);
        }
    }

    private void OnAudioActiveSceneChanged(Scene previous, Scene next)
    {
        ResetFinisherBgmDucking();
        RequestSceneMusic(next);
    }
    private void RequestSceneMusic(Scene scene)
    {
        // A static event may still be dispatching while this component is destroyed.
        if (this == null)
        {
            UnsubscribeSceneEvents();
            return;
        }
        if (_instance != this || !isActiveAndEnabled)
            return;
        if (!scene.IsValid() || !scene.isLoaded)
            return;
        AudioSceneKey key = AudioLifetimeRegistry.ForScene(scene);
        if (key == _sceneMusicKey)
            return;
        _sceneMusicKey = key;
        string sceneName = scene.name;
        if (sceneName.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            PlayMusicEvent("Music.Title", scene);
        }
        else if (sceneName.IndexOf("Ep0", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 sceneName.IndexOf("Prologue", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            // EP0 음악은 문을 열고 나온 시점에 연출 컨트롤러가 시작한다.
            StopBgm(0f);
        }
        else if (sceneName.StartsWith("EP", StringComparison.OrdinalIgnoreCase) ||
                         sceneName.StartsWith("Test_Ep", StringComparison.OrdinalIgnoreCase) || sceneName == "Test_StageField")
        {
            PlayMusicEvent("Music.Field", scene);
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
        {
            _bgmSource.volume = (_isBgmMuted ? 0f : _bgmVolume) * _musicVolume * _musicFade * _finisherBgmGain;
        }
    }

    private void HandleFinisherAudioAnticipation(Transform attacker, Transform target, float progress)
    {
        if (!_enableCombatBgmDucking || attacker == null || target == null) return;
        if (!_isFinisherAudioPreparing)
        {
            _finisherAnticipationStartGain = _finisherBgmGain;
            _isFinisherAudioPreparing = true;
        }
        _isFinisherDuckRecovering = false;
        float blend = Mathf.InverseLerp(_finisherDuckStartProgress, 1f, progress);
        float gain = Mathf.Lerp(_finisherAnticipationStartGain, _finisherDuckedVolume, Mathf.SmoothStep(0f, 1f, blend));
        _finisherBgmGain = Mathf.Min(_finisherBgmGain, gain);
        ApplyBgmVolume();
    }

    private void HandleFinisherAudioImpact(Transform attacker, Transform target)
    {
        if (!_enableCombatBgmDucking || attacker == null) return;
        StartCombatBgmDuckRecovery(_finisherDuckedVolume, _finisherDuckRecoveryTime);
    }

    public void DuckBgmForParry()
    {
        if (!_enableCombatBgmDucking || !isActiveAndEnabled) return;
        StartCombatBgmDuckRecovery(_parryDuckedVolume, _parryDuckRecoveryTime);
    }

    private void StartCombatBgmDuckRecovery(float volume, float recoveryTime)
    {
        _isFinisherAudioPreparing = false;
        _activeDuckRecoveryTime = recoveryTime;
        _finisherBgmGain = Mathf.Min(_finisherBgmGain, volume);
        _finisherRecoveryStartGain = _finisherBgmGain;
        _finisherDuckElapsed = -_finisherDuckHoldTime;
        _isFinisherDuckRecovering = true;
        ApplyBgmVolume();
    }

    private void HandleFinisherAudioCancelled()
    {
        _isFinisherAudioPreparing = false;
        _activeDuckRecoveryTime = _finisherDuckRecoveryTime;
        _finisherRecoveryStartGain = _finisherBgmGain;
        _finisherDuckElapsed = 0f;
        _isFinisherDuckRecovering = true;
    }

    private void UpdateFinisherBgmDucking()
    {
        if (!_enableCombatBgmDucking)
        {
            if (_finisherBgmGain < 1f || _isFinisherDuckRecovering) ResetFinisherBgmDucking();
            return;
        }
        if (!_isFinisherDuckRecovering || PauseController.IsPaused) return;
        _finisherDuckElapsed += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(_finisherDuckElapsed / Mathf.Max(0.01f, _activeDuckRecoveryTime));
        _finisherBgmGain = Mathf.Lerp(_finisherRecoveryStartGain, 1f, Mathf.SmoothStep(0f, 1f, progress));
        if (progress >= 1f) _isFinisherDuckRecovering = false;
        ApplyBgmVolume();
    }

    private void ResetFinisherBgmDucking()
    {
        _finisherBgmGain = 1f;
        _isFinisherAudioPreparing = false;
        _isFinisherDuckRecovering = false;
        ApplyBgmVolume();
    }

    private void ApplySfxVolume()
    {
        foreach (AudioSource source in _sfxPool)
        {
            float gain = 1f;
            if (_channels.TryGetValue(source, out Request request))
            {
                gain = request.Volume * request.Envelope;
            }
            source.volume = (_isSfxMuted ? 0f : _sfxVolume) * gain;
        }
        ApplyDedicatedSfxVolume();
    }

    private void ApplyDedicatedSfxVolume()
    {
        foreach (Request request in _channels.Values)
        {
            if (request.DedicatedChannel && request.Source != null)
                request.Source.volume = (_isSfxMuted ? 0f : _sfxVolume) * request.Volume * request.Envelope;
        }
    }

    [Header("통합 오디오")]
    [SerializeField] private AudioCatalogSO _catalog;
    [SerializeField, Min(0f)] private float _defaultBgmFade = 0.5f;
    public AudioCatalogSO Catalog => _catalog;

    private sealed class Request
    {
        public PlayHandle Handle;
        public int SetId;
        public string EventId, SoundId;
        public AudioContext Context;
        public LifetimePolicy Lifetime;
        public AudioTimeMode TimeMode;
        public IntervalScope IntervalScope;
        public AudioClip Clip;
        public float Volume, Pitch, Delay, Interval, Fade;
        public float FullVolumeDuration = -1f;
        public float EndFadeDuration, PlaybackElapsed;
        public float Envelope = 1f;
        public int Priority, MaxCount, StartSample;
        public bool Loop, Bgm, Started, Paused, DedicatedChannel;
        public ulong MusicSequence, StartOrder;
        public AudioSource Source;
        public Coroutine Pending;
    }

    private static ulong _nextRequestId;
    private static uint _nextManagerGeneration;
    private uint _handleGeneration;
    private ulong _musicSequence, _startOrder;
    private float _musicVolume = 1f, _musicFade = 1f;
    private Request _pendingMusic, _currentMusic;
    private readonly Dictionary<ulong, Request> _requests = new Dictionary<ulong, Request>();
    private readonly Dictionary<AudioSource, Request> _channels = new Dictionary<AudioSource, Request>();
    private readonly Dictionary<(AudioClip, int), AudioClip> _trimmedLoops = new Dictionary<(AudioClip, int), AudioClip>();
    private readonly HashSet<AudioClip> _warnedTrimClips = new HashSet<AudioClip>();
    private readonly Dictionary<int, uint> _ownerGenerations = new Dictionary<int, uint>();
    private readonly Dictionary<int, uint> _sceneGenerations = new Dictionary<int, uint>();
    private readonly HashSet<AudioOwnerKey> _releasedOwners = new HashSet<AudioOwnerKey>();
    private readonly HashSet<AudioSceneKey> _releasedScenes = new HashSet<AudioSceneKey>();
    private readonly HashSet<(AudioOwnerKey, ulong)> _cancelledActions = new HashSet<(AudioOwnerKey, ulong)>();
    private readonly HashSet<(AudioOwnerKey, ulong)> _completedActions = new HashSet<(AudioOwnerKey, ulong)>();
    private readonly Dictionary<(int, string, AudioOwnerKey), (double Game, double Real)> _lastStarts =
        new Dictionary<(int, string, AudioOwnerKey), (double Game, double Real)>();
    private readonly List<PlayHandle> _cleanup = new List<PlayHandle>();

    private void InitializeRuntime()
    {
        _handleGeneration = ++_nextManagerGeneration;
        if (_handleGeneration == 0)
        {
            _handleGeneration = ++_nextManagerGeneration;
        }
        ApplySfxVolume();
    }

    public PlayHandle PlayEvent(SoundSetSO set, string eventId, AudioContext context)
        => PlayEventCore(set, eventId, context);

    private PlayHandle PlayEventCore(SoundSetSO set, string eventId, AudioContext context,
        AudioTimeMode? timeOverride = null, LifetimePolicy? requiredLifetime = null, AudioOutputGroup? requiredOutput = null, AudioClip excludedClip = null, bool dedicatedChannel = false)
    {
        if (this == null || _instance != this || !isActiveAndEnabled || string.IsNullOrWhiteSpace(eventId) || set == null || !set.TryGetEvent(eventId, out EventBinding binding) ||
            binding == null || !binding.Enabled || !binding.HasValidLifetime || !context.Supports(binding.LifetimePolicy) ||
            (requiredLifetime.HasValue && binding.LifetimePolicy != requiredLifetime.Value))
        {
            return PlayHandle.Empty;
        }

        SoundData sound;
        if (!set.TryGetSound(binding.SoundId, out sound) &&
            (_catalog == null || !_catalog.TryGetSound(binding.SoundId, out sound)))
        {
            return PlayHandle.Empty;
        }

        if (sound == null || string.IsNullOrWhiteSpace(sound.SoundId) || !sound.HasValidClip || !sound.HasValidStartOffset ||
            sound.PitchMin <= 0f || sound.PitchMax < sound.PitchMin ||
            float.IsNaN(sound.PitchMin) || float.IsNaN(sound.PitchMax) || float.IsInfinity(sound.PitchMax) ||
            (requiredOutput.HasValue && sound.OutputGroup != requiredOutput.Value))
        {
            return PlayHandle.Empty;
        }

        if (!AcceptContext(context, binding.LifetimePolicy))
            return PlayHandle.Empty;
        (AudioOwnerKey, ulong) action = (context.Owner, context.ActionExecutionId);
        if (binding.LifetimePolicy == LifetimePolicy.Action &&
            (_cancelledActions.Contains(action) || (binding.Loop && _completedActions.Contains(action))))
        {
            return PlayHandle.Empty;
        }

        int setId = set.GetInstanceID();
        if (binding.Loop)
        {
            foreach (Request existing in _requests.Values)
            {
                if (existing.Loop && existing.SetId == setId && existing.EventId == eventId &&
                                existing.Context.Owner == context.Owner && existing.Context.Scene == context.Scene &&
                                existing.Context.ActionExecutionId == context.ActionExecutionId)
                {
                    return existing.Handle;
                }
            }
        }

        // The trim point must leave at least one sample in the selected clip.
        int count = 0;
        foreach (AudioClip clip in sound.Clips)
        {
            if (clip != excludedClip && sound.TryGetStartSample(clip, out _)) count++;
        }
        if (count == 0 && excludedClip != null)
        {
            excludedClip = null;
            foreach (AudioClip clip in sound.Clips)
                if (sound.TryGetStartSample(clip, out _)) count++;
        }
        if (count == 0) return PlayHandle.Empty;
        int choice = UnityEngine.Random.Range(0, count);
        AudioClip selected = null;
        foreach (AudioClip clip in sound.Clips)
        {
            if (clip != excludedClip && sound.TryGetStartSample(clip, out _) && choice-- == 0)
            {
                selected = clip;
                break;
            }
        }
        sound.TryGetStartSample(selected, out int startSample);
        var request = new Request
        {
            StartSample = startSample,
            SetId = setId,
            EventId = eventId,
            SoundId = sound.SoundId,
            Context = context,
            Lifetime = binding.LifetimePolicy,
            TimeMode = timeOverride ?? binding.TimeMode,
            IntervalScope = binding.IntervalScope,
            Clip = selected,
            Volume = Mathf.Clamp01(sound.Volume),
            Pitch = UnityEngine.Random.Range(sound.PitchMin, sound.PitchMax),
            Delay = binding.EffectiveDelay,
            Interval = binding.EffectiveMinInterval,
            Fade = binding.BgmFadeDuration < 0f ? _defaultBgmFade : binding.BgmFadeDuration,
            Priority = Mathf.Clamp(sound.Priority, 0, 100),
            MaxCount = Mathf.Max(1, sound.MaxCount),
            Loop = dedicatedChannel || binding.Loop,
            DedicatedChannel = dedicatedChannel,
            Bgm = sound.OutputGroup == AudioOutputGroup.Bgm
        };
        AddRequest(request);
        if (request.Bgm)
        {
            BeginMusicRequest(request);
        }
        Schedule(request);
        return IsHandleValid(request.Handle) ? request.Handle : PlayHandle.Empty;
    }

    private void AddRequest(Request request)
    {
        ulong id = ++_nextRequestId;
        if (id == 0)
        {
            id = ++_nextRequestId;
        }
        request.Handle = new PlayHandle(id, _handleGeneration);
        _requests.Add(id, request);
    }

    public bool IsHandleValid(PlayHandle handle) => !handle.IsEmpty &&
        _requests.TryGetValue(handle.RequestId, out Request request) && request.Handle == handle;

    public void LimitSfxPlayback(PlayHandle handle, float fullVolumeDuration, float fadeDuration)
    {
        if (!IsHandleValid(handle)) return;
        Request request = _requests[handle.RequestId];
        if (request.Bgm || float.IsNaN(fullVolumeDuration) || float.IsInfinity(fullVolumeDuration) ||
            float.IsNaN(fadeDuration) || float.IsInfinity(fadeDuration))
        {
            return;
        }

        request.FullVolumeDuration = Mathf.Max(0f, fullVolumeDuration);
        request.EndFadeDuration = Mathf.Max(0f, fadeDuration);
    }

    public AudioClip GetHandleClip(PlayHandle handle) => IsHandleValid(handle) ? _requests[handle.RequestId].Clip : null;

    private bool AcceptContext(AudioContext context, LifetimePolicy lifetime)
    {
        if (lifetime != LifetimePolicy.Global)
        {
            if (_releasedScenes.Contains(context.Scene))
                return false;
            if (_sceneGenerations.TryGetValue(context.Scene.SceneHandle, out uint sceneGeneration))
            {
                if (context.Scene.Generation < sceneGeneration)
                    return false;
                if (context.Scene.Generation > sceneGeneration)
                {
                    ReleaseScene(new AudioSceneKey(context.Scene.SceneHandle, sceneGeneration));
                }
            }
            _sceneGenerations[context.Scene.SceneHandle] = context.Scene.Generation;
        }
        if (lifetime == LifetimePolicy.Action || lifetime == LifetimePolicy.Entity)
        {
            if (_releasedOwners.Contains(context.Owner))
                return false;
            if (_ownerGenerations.TryGetValue(context.Owner.OwnerId, out uint ownerGeneration))
            {
                if (context.Owner.Generation < ownerGeneration)
                    return false;
                if (context.Owner.Generation > ownerGeneration)
                {
                    ReleaseOwner(new AudioOwnerKey(context.Owner.OwnerId, ownerGeneration));
                }
            }
            _ownerGenerations[context.Owner.OwnerId] = context.Owner.Generation;
        }
        return true;
    }

    private bool CanStart(Request request)
    {
        if (!IsHandleValid(request.Handle))
            return false;
        if (request.Lifetime != LifetimePolicy.Global && _releasedScenes.Contains(request.Context.Scene))
            return false;
        if (request.Lifetime == LifetimePolicy.Action || request.Lifetime == LifetimePolicy.Entity)
        {
            if (_releasedOwners.Contains(request.Context.Owner))
                return false;
        }
        if (request.Lifetime == LifetimePolicy.Action &&
            _cancelledActions.Contains((request.Context.Owner, request.Context.ActionExecutionId)))
        {
            return false;
        }

        return !request.Bgm || request.MusicSequence == _musicSequence;
    }

    private void Schedule(Request request)
    {
        if (request.Delay <= 0f)
        {
            StartRequest(request);
        }
        else
        {
            request.Pending = StartCoroutine(WaitForRequest(request));
        }
    }

    private IEnumerator WaitForRequest(Request request)
    {
        float remaining = request.Delay;
        while (remaining > 0f)
        {
            yield return null;
            if (!CanStart(request))
            {
                request.Pending = null;
                StopHandle(request.Handle);
                yield break;
            }
            remaining -= request.TimeMode == AudioTimeMode.GameTime ? Time.deltaTime : Time.unscaledDeltaTime;
        }
        request.Pending = null;
        StartRequest(request);
    }

    private (int, string, AudioOwnerKey) IntervalKey(Request request) =>
        (request.SetId, request.EventId, request.IntervalScope == IntervalScope.Global ? default : request.Context.Owner);

    private bool IntervalAllowed(Request request)
    {
        if (request.Interval <= 0f || !_lastStarts.TryGetValue(IntervalKey(request), out (double Game, double Real) last))
            return true;
        return request.TimeMode == AudioTimeMode.GameTime
            ? Time.timeAsDouble - last.Game >= request.Interval
            : Time.unscaledTimeAsDouble - last.Real >= request.Interval;
    }

    private void RecordStart(Request request)
    {
        request.Started = true;
        request.StartOrder = ++_startOrder;
        _lastStarts[IntervalKey(request)] = (Time.timeAsDouble, Time.unscaledTimeAsDouble);
    }

    private bool PreparePlaybackData(Request request)
    {
        AudioClip original = request.Clip;
        if (original.loadState == AudioDataLoadState.Unloaded) original.LoadAudioData();
        if (original.loadState == AudioDataLoadState.Loading)
        {
            request.Pending = StartCoroutine(WaitForAudioData(request));
            return false;
        }
        if (original.loadState != AudioDataLoadState.Loaded)
        {
            StopHandle(request.Handle);
            return false;
        }
        if (request.StartSample == 0 || !request.Loop) return true;
        (AudioClip original, int StartSample) key = (original, request.StartSample);
        if (!_trimmedLoops.TryGetValue(key, out AudioClip trimmed) || trimmed == null)
        {
            int remaining = original.samples - request.StartSample;
            float[] samples = new float[remaining * original.channels];
            if (!original.GetData(samples, request.StartSample))
            {
                if (_warnedTrimClips.Add(original))
                    Debug.LogWarning($"{original.name}: 앞부분을 건너뛰는 반복음은 Audio Import Load Type을 Decompress On Load로 설정하세요.", this);
                StopHandle(request.Handle);
                return false;
            }
            trimmed = AudioClip.Create(original.name + " (trimmed loop)", remaining, original.channels, original.frequency, false);
            trimmed.hideFlags = HideFlags.HideAndDontSave;
            if (!trimmed.SetData(samples, 0))
            {
                Destroy(trimmed);
                StopHandle(request.Handle);
                return false;
            }
            _trimmedLoops[key] = trimmed;
        }
        request.Clip = trimmed;
        request.StartSample = 0;
        return true;
    }

    private IEnumerator WaitForAudioData(Request request)
    {
        while (request.Clip != null && request.Clip.loadState == AudioDataLoadState.Loading)
        {
            yield return null;
            if (!CanStart(request))
            {
                request.Pending = null;
                StopHandle(request.Handle);
                yield break;
            }
        }
        request.Pending = null;
        StartRequest(request);
    }

    private void StartRequest(Request request)
    {
        if (!CanStart(request) || request.Clip == null || !IntervalAllowed(request))
        {
            StopHandle(request.Handle);
            return;
        }
        if (!PreparePlaybackData(request)) return;
        if (request.Bgm)
        {
            _bgmFadeCoroutine = StartCoroutine(TransitionMusic(request, request.Fade, request.MusicSequence));
            return;
        }
        // 종료된 재생을 먼저 회수해 동시 수/채널 검사에 포함하지 않는다.
        ReapFinished();
        int count = 0;
        foreach (Request playing in _channels.Values)
        {
            if (playing.SoundId == request.SoundId)
            {
                count++;
            }
        }
        if (!request.DedicatedChannel && count >= request.MaxCount)
        {
            StopHandle(request.Handle);
            return;
        }
        AudioSource channel = null;
        if (request.DedicatedChannel)
        {
            var channelObject = new GameObject(request.EventId + " channel");
            channelObject.transform.SetParent(transform, false);
            channel = channelObject.AddComponent<AudioSource>();
            channel.playOnAwake = false;
            channel.spatialBlend = 0f;
        }
        foreach (AudioSource source in _sfxPool)
        {
            if (channel == null && !_channels.ContainsKey(source))
            {
                channel = source;
                break;
            }
        }
        if (channel == null)
        {
            Request victim = null;
            foreach (Request playing in _channels.Values)
            {
                if (!playing.DedicatedChannel && playing.Priority < request.Priority && (victim == null || playing.Priority < victim.Priority ||
                                (playing.Priority == victim.Priority && playing.StartOrder < victim.StartOrder)))
                {
                    victim = playing;
                }
            }
            if (victim == null)
            {
                StopHandle(request.Handle);
                return;
            }
            channel = victim.Source;
            StopHandle(victim.Handle);
        }
        request.Source = channel;
        _channels[channel] = request;
        channel.clip = request.Clip;
        channel.timeSamples = request.StartSample;
        channel.pitch = request.Pitch;
        channel.loop = request.Loop;
        channel.volume = (_isSfxMuted ? 0f : _sfxVolume) * request.Volume;
        RecordStart(request);
        channel.Play();
        if (request.TimeMode == AudioTimeMode.GameTime && Time.timeScale == 0f)
        {
            channel.Pause();
            request.Paused = true;
        }
    }

    private void Update()
    {
        UpdateFinisherBgmDucking();
        foreach (Request request in _channels.Values)
        {
            if (request.TimeMode != AudioTimeMode.GameTime)
            {
                continue;
            }
            if (Time.timeScale == 0f && !request.Paused && request.Source.isPlaying)
            {
                request.Source.Pause();
                request.Paused = true;
            }
            else if (Time.timeScale > 0f && request.Paused)
            {
                request.Source.UnPause();
                request.Paused = false;
            }
        }
        UpdateSfxEnvelopes();
        ReapFinished();
        if (_currentMusic != null && _pendingMusic == null && _bgmFadeCoroutine == null && !_bgmSource.isPlaying)
        {
            _requests.Remove(_currentMusic.Handle.RequestId);
            _currentMusic = null;
        }
    }

    private void UpdateSfxEnvelopes()
    {
        foreach (Request request in _channels.Values)
        {
            if (request.Paused || request.FullVolumeDuration < 0f) continue;
            // AudioSource의 실제 재생 속도를 따른다. 일시 정지 중에는 진행하지 않는다.
            request.PlaybackElapsed += Time.unscaledDeltaTime;
            float fadeElapsed = request.PlaybackElapsed - request.FullVolumeDuration;
            if (fadeElapsed < 0f) continue;
            if (request.EndFadeDuration <= 0f || fadeElapsed >= request.EndFadeDuration)
            {
                request.Envelope = 0f;
                request.Source.Stop();
            }
            else
            {
                float remaining = 1f - fadeElapsed / request.EndFadeDuration;
                request.Envelope = remaining * remaining;
            }
            request.Source.volume = (_isSfxMuted ? 0f : _sfxVolume) * request.Volume * request.Envelope;
        }
    }

    private void ReapFinished()
    {
        _cleanup.Clear();
        foreach (Request request in _channels.Values)
        {
            if (!request.Paused && !request.Source.isPlaying)
            {
                _cleanup.Add(request.Handle);
            }
        }
        foreach (PlayHandle handle in _cleanup)
        {
            StopHandle(handle);
        }
        _cleanup.Clear();
    }

    public void StopHandle(PlayHandle handle)
    {
        if (!IsHandleValid(handle))
            return;
        Request request = _requests[handle.RequestId];
        _requests.Remove(handle.RequestId);
        if (request.Pending != null)
        {
            StopCoroutine(request.Pending);
        }
        if (request.Bgm)
        {
            if (_pendingMusic == request)
            {
                _musicSequence++;
                if (_bgmFadeCoroutine != null)
                {
                    StopCoroutine(_bgmFadeCoroutine);
                }
                _bgmFadeCoroutine = null;
                _pendingMusic = null;
                _musicFade = 1f;
                ApplyBgmVolume();
            }
            if (_currentMusic == request)
            {
                if (_pendingMusic == null)
                {
                    _musicSequence++;
                    if (_bgmFadeCoroutine != null)
                    {
                        StopCoroutine(_bgmFadeCoroutine);
                    }
                    _bgmFadeCoroutine = null;
                }
                _bgmSource.Stop();
                _bgmSource.clip = null;
                _currentMusic = null;
            }
        }
        else if (request.Source != null && _channels.TryGetValue(request.Source, out Request occupying) && occupying == request)
        {
            _channels.Remove(request.Source);
            request.Source.Stop();
            request.Source.clip = null;
            if (request.DedicatedChannel) Destroy(request.Source.gameObject);
        }
    }

    private void StopMatching(Predicate<Request> predicate)
    {
        // 호출 중 코루틴/채널 정리와 목록 변경을 안전하게 처리한다.
        var handles = new List<PlayHandle>();
        foreach (Request request in _requests.Values)
        {
            if (predicate(request))
            {
                handles.Add(request.Handle);
            }
        }
        foreach (PlayHandle handle in handles)
        {
            StopHandle(handle);
        }
    }

    public void CompleteAction(AudioOwnerKey owner, ulong actionExecutionId)
    {
        if (owner.IsEmpty || actionExecutionId == 0)
            return;
        _completedActions.Add((owner, actionExecutionId));
        StopMatching(r => r.Lifetime == LifetimePolicy.Action && r.Context.Owner == owner &&
            r.Context.ActionExecutionId == actionExecutionId && r.Loop);
    }

    public void CancelAction(AudioOwnerKey owner, ulong actionExecutionId)
    {
        if (owner.IsEmpty || actionExecutionId == 0)
            return;
        _cancelledActions.Add((owner, actionExecutionId));
        StopMatching(r => r.Lifetime == LifetimePolicy.Action && r.Context.Owner == owner &&
            r.Context.ActionExecutionId == actionExecutionId && (!r.Started || r.Loop));
    }

    public void ReleaseOwner(AudioOwnerKey owner)
    {
        if (owner.IsEmpty)
            return;
        _releasedOwners.Add(owner);
        StopMatching(r => r.Context.Owner == owner &&
            (r.Lifetime == LifetimePolicy.Action || r.Lifetime == LifetimePolicy.Entity));
        _cancelledActions.RemoveWhere(key => key.Item1 == owner);
        _completedActions.RemoveWhere(key => key.Item1 == owner);
        ClearOwnerIntervals(owner);
    }

    private void ClearOwnerIntervals(AudioOwnerKey owner)
    {
        var keys = new List<(int, string, AudioOwnerKey)>();
        foreach ((int, string, AudioOwnerKey) key in _lastStarts.Keys)
        {
            if (key.Item3 == owner)
            {
                keys.Add(key);
            }
        }
        foreach ((int, string, AudioOwnerKey) key in keys)
        {
            _lastStarts.Remove(key);
        }
    }

    public void ReleaseScene(AudioSceneKey scene)
    {
        if (scene.IsEmpty)
            return;
        _releasedScenes.Add(scene);
        // 재생 중 BGM은 다음 음악 요청/StopBgm의 페이드 경로에 맡긴다.
        StopMatching(r => r.Lifetime != LifetimePolicy.Global && r.Context.Scene == scene && (!r.Bgm || !r.Started));
        // A newer scene request owns its transition; never stop it from the old scene.
        if (_currentMusic != null && _currentMusic.Context.Scene == scene && _pendingMusic == null)
        {
            StopBgm(_defaultBgmFade);
        }
    }

    private void BeginMusicRequest(Request request)
    {
        if (_pendingMusic != null)
        {
            StopHandle(_pendingMusic.Handle);
        }
        if (_bgmFadeCoroutine != null)
        {
            StopCoroutine(_bgmFadeCoroutine);
        }
        _bgmFadeCoroutine = null;
        // 새 요청이 지연되는 동안 이전 곡이 중간 페이드 음량에 머물지 않게 한다.
        _musicFade = 1f;
        ApplyBgmVolume();
        request.MusicSequence = ++_musicSequence;
        _pendingMusic = request;
    }

    private IEnumerator TransitionMusic(Request request, float duration, ulong sequence)
    {
        // 0초 전환에서도 Coroutine 필드가 종료 후 남지 않도록 한 번 yield한다.
        float startFade = _musicFade;
        if (_bgmSource.isPlaying && duration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (sequence != _musicSequence)
                {
                    yield break;
                }
                elapsed += Time.unscaledDeltaTime;
                _musicFade = Mathf.Lerp(startFade, 0f, elapsed / duration);
                ApplyBgmVolume();
                yield return null;
            }
        }
        if (sequence != _musicSequence)
        {
            yield break;
        }
        if (request != null && (!CanStart(request) || !IntervalAllowed(request)))
        {
            _bgmFadeCoroutine = null;
            StopHandle(request.Handle);
            yield break;
        }
        _bgmSource.Stop();
        if (_currentMusic != null)
        {
            _requests.Remove(_currentMusic.Handle.RequestId);
        }
        _currentMusic = request;
        _pendingMusic = null;
        _bgmSource.clip = request?.Clip;
        if (request != null)
        {
            request.Source = _bgmSource;
            _musicVolume = request.Volume;
            _bgmSource.timeSamples = request.StartSample;
            _bgmSource.pitch = request.Pitch;
            _bgmSource.loop = request.Loop;
            _musicFade = duration > 0f ? 0f : 1f;
            ApplyBgmVolume();
            RecordStart(request);
            _bgmSource.Play();
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (sequence != _musicSequence)
                {
                    yield break;
                }
                elapsed += Time.unscaledDeltaTime;
                _musicFade = Mathf.Clamp01(elapsed / duration);
                ApplyBgmVolume();
                yield return null;
            }
        }
        yield return null;
        if (sequence != _musicSequence)
        {
            yield break;
        }
        _musicFade = 1f;
        ApplyBgmVolume();
        _bgmFadeCoroutine = null;
    }

    private void BeginMusicStop(float duration)
    {
        if (_pendingMusic != null)
        {
            StopHandle(_pendingMusic.Handle);
        }
        if (_bgmFadeCoroutine != null)
        {
            StopCoroutine(_bgmFadeCoroutine);
        }
        ulong sequence = ++_musicSequence;
        _bgmFadeCoroutine = StartCoroutine(TransitionMusic(null, duration, sequence));
    }

    private void RequestLegacyBgm(AudioClip clip, float fade)
    {
        if (_pendingMusic == null && _bgmFadeCoroutine == null && _bgmSource.clip == clip && _bgmSource.isPlaying)
            return;
        var request = new Request
        {
            Clip = clip,
            Volume = 1f,
            Pitch = 1f,
            Bgm = true,
            Loop = true,
            Lifetime = LifetimePolicy.Global,
            TimeMode = AudioTimeMode.RealTime,
            Fade = Mathf.Max(0f, fade)
        };
        AddRequest(request);
        BeginMusicRequest(request);
        Schedule(request);
    }

    private AudioSource PlayLegacySfx(AudioClip clip, float volume)
    {
        var request = new Request
        {
            Clip = clip,
            Volume = volume,
            Pitch = 1f,
            SoundId = "legacy:" + clip.GetInstanceID(),
            Priority = 50,
            MaxCount = 4,
            Lifetime = LifetimePolicy.Global,
            TimeMode = AudioTimeMode.RealTime
        };
        AddRequest(request);
        StartRequest(request);
        return IsHandleValid(request.Handle) ? request.Source : null;
    }

    private void UnsubscribeSceneEvents()
    {
        SceneManager.sceneLoaded -= OnAudioSceneLoaded;
        SceneManager.activeSceneChanged -= OnAudioActiveSceneChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnFinisherAnticipation -= HandleFinisherAudioAnticipation;
        GameEvents.OnFinisherImpact -= HandleFinisherAudioImpact;
        GameEvents.OnFinisherAnticipationCancelled -= HandleFinisherAudioCancelled;
        ResetFinisherBgmDucking();
        UnsubscribeSceneEvents();
        _sceneMusicKey = default;
        StopAllCoroutines();
        foreach (AudioSource source in _sfxPool)
        {
            if (source != null)
            {
                source.Stop();
            }
        }
        if (_bgmSource != null)
        {
            _bgmSource.Stop();
        }
        _requests.Clear();
        _channels.Clear();
        _pendingMusic = _currentMusic = null;
        _bgmFadeCoroutine = null;
        _musicSequence++;
    }

    private void OnApplicationQuit()
    {
        if (_instance == this)
        {
            _isShuttingDown = true;
        }
    }

    private void OnDestroy()
    {
        UnsubscribeSceneEvents();
        foreach (AudioClip clip in _trimmedLoops.Values)
        {
            if (clip != null) Destroy(clip);
        }
        _trimmedLoops.Clear();
        if (_instance == this)
        {
            _isShuttingDown = true;
            _instance = null;
        }
    }
}
