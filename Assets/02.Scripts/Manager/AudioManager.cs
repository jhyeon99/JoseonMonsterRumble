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

    private void OnAudioActiveSceneChanged(Scene previous, Scene next) => RequestSceneMusic(next);
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
            _bgmSource.volume = (_isBgmMuted ? 0f : _bgmVolume) * _musicVolume * _musicFade;
        }
    }

    private void ApplySfxVolume()
    {
        foreach (AudioSource source in _sfxPool)
        {
            float gain = 1f;
            if (_channels.TryGetValue(source, out Request request))
            {
                gain = request.Volume;
            }
            source.volume = (_isSfxMuted ? 0f : _sfxVolume) * gain;
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
        public int Priority, MaxCount;
        public bool Loop, Bgm, Started, Paused;
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
        AudioTimeMode? timeOverride = null, LifetimePolicy? requiredLifetime = null, AudioOutputGroup? requiredOutput = null)
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

        if (sound == null || string.IsNullOrWhiteSpace(sound.SoundId) || !sound.HasValidClip ||
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

        // null 클립을 제외한 균등 랜덤 선택. 이후 데이터 변경은 이 요청에 영향을 주지 않는다.
        int count = 0;
        foreach (AudioClip clip in sound.Clips)
        {
            if (clip != null)
            {
                count++;
            }
        }
        int choice = UnityEngine.Random.Range(0, count);
        AudioClip selected = null;
        foreach (AudioClip clip in sound.Clips)
        {
            if (clip != null && choice-- == 0)
            {
                selected = clip;
                break;
            }
        }
        var request = new Request
        {
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
            Loop = binding.Loop,
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

    private void StartRequest(Request request)
    {
        if (!CanStart(request) || request.Clip == null || !IntervalAllowed(request))
        {
            StopHandle(request.Handle);
            return;
        }
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
        if (count >= request.MaxCount)
        {
            StopHandle(request.Handle);
            return;
        }
        AudioSource channel = null;
        foreach (AudioSource source in _sfxPool)
        {
            if (!_channels.ContainsKey(source))
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
                if (playing.Priority < request.Priority && (victim == null || playing.Priority < victim.Priority ||
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
        ReapFinished();
        if (_currentMusic != null && _pendingMusic == null && _bgmFadeCoroutine == null && !_bgmSource.isPlaying)
        {
            _requests.Remove(_currentMusic.Handle.RequestId);
            _currentMusic = null;
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

    private void OnDestroy()
    {
        UnsubscribeSceneEvents();
        if (_instance == this)
        {
            _instance = null;
        }
    }
}
