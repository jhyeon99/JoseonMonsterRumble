using System;
using System.Collections.Generic;
using GameAudio;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AudioManager))]
public sealed class AudioManagerEditor : Editor
{
    private readonly Dictionary<UnityEngine.Object, SerializedObject> _assets = new Dictionary<UnityEngine.Object, SerializedObject>();
    private readonly Dictionary<string, bool> _foldouts = new Dictionary<string, bool>();
    private string _previewError;
    private bool _showWarnings;
    private AudioCatalogSO _catalog;
    private EnemyData _previewMonster;

    private void OnEnable() => Undo.undoRedoPerformed += OnUndoRedo;
    private void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
        AudioEditorPreview.Stop();
        foreach (SerializedObject asset in _assets.Values) asset.Dispose();
        _assets.Clear();
    }

    private void OnUndoRedo()
    {
        // 재생 중의 볼륨 Undo도 실제 채널과 저장값에 반영한다.
        if (Application.isPlaying && target != null)
        {
            var manager = (AudioManager)target;
            manager.SetBgmVolume(manager.GetBgmVolume());
            manager.SetSfxVolume(manager.GetSfxVolume());
            manager.SetBgmMute(manager.IsBgmMuted());
            manager.SetSfxMute(manager.IsSfxMuted());
        }
        SaveDirtyAudioAssets();
        Repaint();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour((AudioManager)target), typeof(MonoScript), false);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_catalog"), new GUIContent("Audio Catalog"));
        serializedObject.ApplyModifiedProperties();
        _catalog = ((AudioManager)target).Catalog;
        UpdateMusicLabels();
        UpdateBossEggLabels();
        ConfigureBabyCrawlLoop();
        EnsureSound(_catalog?.Boss, "Boss.BabyCrawl", "올챙이 바닥 기어다니기", LifetimePolicy.Entity, 0.2f, 0.95f, 1.05f, 3, 20);
        EnsureSound(_catalog?.Player, "Player.FinisherDisappear", "날아간 몬스터 소멸 반짝임", LifetimePolicy.Result, 1f, 1f, 1f, 4, 50);
        EnsureSound(_catalog?.Boss, "Boss.Tail", "보스 꼬리 휘두르기", LifetimePolicy.Action, 1f, 1f, 1f, 4, 50);
        EnsureSound(_catalog?.Boss, "Boss.TailImpact", "보스 꼬리 공격 실제 명중", LifetimePolicy.Result, 1f, 1f, 1f, 4, 50);
        EnsureSound(_catalog?.Boss, "Boss.GroggyHurt", "그로기 상태 보스 피격", LifetimePolicy.Result, 1f, 1f, 1f, 4, 50);
        EnsureSound(_catalog?.Boss, "Boss.StompFootstep", "보스 찍기 발바닥", LifetimePolicy.Result, 1f, 1f, 1f, 4, 50);
        EnsureSound(_catalog?.Boss, "Boss.LandingBreak", "보스 착지 무너짐", LifetimePolicy.Result, 1f, 1f, 1f, 4, 50);
        EnsureSound(_catalog?.Boss, "Boss.Footstep", "보스 이동 착지 발바닥", LifetimePolicy.Result, 1f, 1f, 1f, 4, 50);
        EnsureSound(_catalog?.Boss, "Boss.FrogAmbience", "보스전 배경 개구리 울음", LifetimePolicy.Entity, 0.2f, 0.95f, 1.05f, 1, 20);

        List<string> warnings = AudioCatalogValidation.Validate(_catalog);
        if (warnings.Count > 0)
        {
            EditorGUILayout.HelpBox($"오디오 데이터 검사: {warnings.Count}개 항목을 확인하세요. 정상 항목의 재생은 계속됩니다.", MessageType.Warning);
            _showWarnings = EditorGUILayout.Foldout(_showWarnings, "검사 상세", true);
            if (_showWarnings) foreach (string warning in warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }
        if (!string.IsNullOrEmpty(_previewError)) EditorGUILayout.HelpBox(_previewError, MessageType.Warning);
        DrawGlobalSettings();
        if (_catalog == null) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("음원과 오디오 데이터 설정은 변경 즉시 자동 저장됩니다.", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.HelpBox(Application.isPlaying
            ? "Play Mode: 카탈로그/SoundSet 편집은 실제 에셋에 저장됩니다. 매니저 참조·페이드 설정은 임시 변경이며, 글로벌 음량·음소거는 사용자 설정으로 저장됩니다."
            : "카탈로그와 SoundSet을 여기서 직접 편집합니다. Undo/Redo를 지원하며, 미리듣기는 항목 볼륨·피치만 사용하고 사용자 설정을 바꾸지 않습니다 (최대 15초).", MessageType.Info);
        if (GUILayout.Button("카탈로그와 SoundSet 에셋 저장"))
        {
            AssetDatabase.SaveAssetIfDirty(_catalog);
            foreach (SoundSetSO set in new HashSet<SoundSetSO>(_catalog.EnumerateSets())) AssetDatabase.SaveAssetIfDirty(set);
        }
        var catalogObject = AssetObject(_catalog);
        DrawCategory(catalogObject, "Player", "플레이어", "Player");
        if (Foldout("Enemy", "몬스터"))
        {
            EditorGUI.indentLevel++;
            if (Foldout("LaunchDeathAudio", "날아가며 사망할 때 사망음 종료"))
            {
                EditorGUILayout.HelpBox("패링 후 마무리 등으로 날아가며 죽는 경우에만 적용합니다. 기존 사망음 연결을 사용하고, 일반 사망은 끝까지 재생합니다.", MessageType.Info);
                EditorGUILayout.PropertyField(catalogObject.FindProperty("LaunchDeathSoundDuration"), new GUIContent("페이드 시작 시점 (재생 후 초)"));
                EditorGUILayout.PropertyField(catalogObject.FindProperty("LaunchDeathSoundFade"), new GUIContent("페이드 지속 시간 (초)"));
                ApplyAssetChanges(catalogObject);
            }
            DrawMonsterProfiles(catalogObject);
            if (Foldout("EnemyDefaults", "미등록 몬스터의 기본 설정"))
            {
                DrawCategory(catalogObject, "Ground.Melee", "지상 / 근접 기본값", "Ground");
                DrawCategory(catalogObject, "Ground.Ranged", "지상 / 원거리 기본값", "Ground");
                DrawCategory(catalogObject, "Air.Melee", "공중 / 근접 기본값", "Air");
                DrawCategory(catalogObject, "Air.Ranged", "공중 / 원거리 기본값", "Air");
                DrawCategory(catalogObject, "Buff", "버프형 기본값", "Buff");
            }
            EditorGUI.indentLevel--;
        }
        DrawCategory(catalogObject, "Boss", "보스", "Boss");
        EditorGUILayout.PropertyField(catalogObject.FindProperty("BossFootstepLeadTime"), new GUIContent("착지음 선행 시간 (초)", "발이 바닥에 닿기 전에 음원을 처음부터 재생합니다. 0이면 실제 착지 시점입니다."));
        ApplyAssetChanges(catalogObject);
        if (Foldout("BossFrogInterval", "보스전 개구리 울음 간격"))
        {
            EditorGUILayout.HelpBox("보스전 시작 후 랜덤 간격으로 한 번씩 울고, 울음이 끝나면 다시 쉽니다. Boss.FrogAmbience에 음원을 넣으세요. 수명은 Entity, 반복 재생은 꺼짐으로 설정합니다.", MessageType.Info);
            EditorGUILayout.PropertyField(catalogObject.FindProperty("BossFrogMinInterval"), new GUIContent("최소 휴식 시간 (초)"));
            EditorGUILayout.PropertyField(catalogObject.FindProperty("BossFrogMaxInterval"), new GUIContent("최대 휴식 시간 (초)"));
            ApplyAssetChanges(catalogObject);
        }
        EditorGUILayout.HelpBox("올챙이 바닥 소리는 Boss.BabyCrawl에 넣습니다. 각 올챙이가 생성되면 목록 중 하나를 랜덤으로 골라 전용 채널로 반복하며, 발사·사망·비활성화 시 정지합니다. 낙하 중이거나 서 있어도 재생하고 휴식 간격은 적용하지 않습니다.", MessageType.Info);
        DrawCategory(catalogObject, "Music", "BGM", "Music");
        DrawCategory(catalogObject, "Item", "아이템", "Item");
        DrawCategory(catalogObject, "Environment", "환경", "Environment");
        DrawCategory(catalogObject, "UI", "UI", "UI");
        DrawCategory(catalogObject, "Prologue", "프롤로그", "Prologue");
    }

    private void ConfigureBabyCrawlLoop()
    {
        SoundSetSO boss = _catalog != null ? _catalog.Boss : null;
        if (boss == null) return;
        SerializedObject data = AssetObject(boss);
        SerializedProperty events = data.FindProperty("_events");
        for (int i = 0; i < events.arraySize; i++)
        {
            SerializedProperty entry = events.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("EventId").stringValue != "Boss.BabyCrawl") continue;
            entry.FindPropertyRelative("Loop").boolValue = true;
            entry.FindPropertyRelative("LoopEndRule").stringValue = "날려 보내기 / 사망 / 비활성화";
            entry.FindPropertyRelative("TriggerDescription").stringValue = AudioEventEditorDefinitions.Description("Boss.BabyCrawl");
        }
        ApplyAssetChanges(data);
    }

    private void UpdateBossEggLabels()
    {
        SoundSetSO boss = _catalog != null ? _catalog.Boss : null;
        if (boss == null) return;
        SerializedObject data = AssetObject(boss);
        // Unity에 로드된 최신 음원 연결을 그대로 유지하며 표시 문구만 바꾼다.
        foreach (string arrayName in new[] { "_sounds", "_events" })
        {
            SerializedProperty entries = data.FindProperty(arrayName);
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                SerializedProperty label = entry.FindPropertyRelative(arrayName == "_sounds" ? "DisplayName" : "TriggerDescription");
                string renamed = label.stringValue.Replace("독 방울", "개구리알").Replace("독방울", "개구리알").Replace("방울", "개구리알");
                if (renamed != label.stringValue) label.stringValue = renamed;
            }
        }
        ApplyAssetChanges(data);
    }

    private void UpdateMusicLabels()
    {
        SoundSetSO music = _catalog != null ? _catalog.Music : null;
        if (music == null) return;
        SerializedObject data = AssetObject(music);
        // 로드된 에셋의 기존 음원 연결과 재생 설정을 보존하고 표시 문구만 갱신한다.
        foreach (string arrayName in new[] { "_sounds", "_events" })
        {
            SerializedProperty items = data.FindProperty(arrayName);
            for (int i = 0; i < items.arraySize; i++)
            {
                SerializedProperty item = items.GetArrayElementAtIndex(i);
                string id = item.FindPropertyRelative(arrayName == "_sounds" ? "SoundId" : "EventId").stringValue;
                string previous = id == "Music.Field" ? "일반 플레이 씬 진입" :
                    id == "Music.Prologue" ? "프롤로그 연출 시작" : null;
                if (previous == null) continue;
                SerializedProperty label = item.FindPropertyRelative(arrayName == "_sounds" ? "DisplayName" : "TriggerDescription");
                if (label.stringValue == previous || string.IsNullOrEmpty(label.stringValue))
                    label.stringValue = AudioEventEditorDefinitions.Description(id);
            }
        }
        ApplyAssetChanges(data);
    }

    private void EnsureSound(SoundSetSO set, string eventId, string label, LifetimePolicy lifetime,
        float volume, float pitchMin, float pitchMax, int maxCount, int priority)
    {
        if (set == null) return;
        bool hasEvent = set.TryGetEvent(eventId, out EventBinding binding);
        if (eventId == "Boss.Footstep" && hasEvent &&
            binding.TriggerDescription == "기본 이동·후퇴 점프의 실제 착지마다 1회")
        {
            SerializedObject existing = AssetObject(set);
            SerializedProperty events = existing.FindProperty("_events");
            for (int i = 0; i < events.arraySize; i++)
            {
                SerializedProperty entry = events.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("EventId").stringValue == eventId)
                    entry.FindPropertyRelative("TriggerDescription").stringValue = AudioEventEditorDefinitions.Description(eventId);
            }
            ApplyAssetChanges(existing);
        }
        string soundId = hasEvent ? binding.SoundId : eventId;
        if (string.IsNullOrWhiteSpace(soundId)) return;
        bool hasSound = set.TryGetSound(soundId, out _);
        if (hasEvent && hasSound) return;

        // Unity에 로드된 최신 연결을 보존하고, 없는 항목만 추가한다.
        SerializedObject data = AssetObject(set);
        if (!hasSound)
        {
            SerializedProperty sounds = data.FindProperty("_sounds");
            int index = sounds.arraySize;
            sounds.InsertArrayElementAtIndex(index);
            SerializedProperty sound = sounds.GetArrayElementAtIndex(index);
            sound.FindPropertyRelative("SoundId").stringValue = soundId;
            sound.FindPropertyRelative("DisplayName").stringValue = label;
            sound.FindPropertyRelative("Clips").ClearArray();
            sound.FindPropertyRelative("StartOffset").floatValue = 0f;
            sound.FindPropertyRelative("Volume").floatValue = volume;
            sound.FindPropertyRelative("PitchMin").floatValue = pitchMin;
            sound.FindPropertyRelative("PitchMax").floatValue = pitchMax;
            sound.FindPropertyRelative("OutputGroup").enumValueIndex = (int)AudioOutputGroup.Sfx;
            sound.FindPropertyRelative("Priority").intValue = priority;
            sound.FindPropertyRelative("MaxCount").intValue = maxCount;
        }
        if (!hasEvent)
        {
            SerializedProperty events = data.FindProperty("_events");
            int index = events.arraySize;
            events.InsertArrayElementAtIndex(index);
            SerializedProperty entry = events.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("EventId").stringValue = eventId;
            entry.FindPropertyRelative("Enabled").boolValue = true;
            entry.FindPropertyRelative("SoundId").stringValue = soundId;
            entry.FindPropertyRelative("TriggerDescription").stringValue = AudioEventEditorDefinitions.Description(eventId);
            entry.FindPropertyRelative("Delay").floatValue = 0f;
            entry.FindPropertyRelative("TimeMode").enumValueIndex = (int)AudioTimeMode.GameTime;
            entry.FindPropertyRelative("LifetimePolicy").enumValueIndex = (int)lifetime;
            entry.FindPropertyRelative("Loop").boolValue = false;
            entry.FindPropertyRelative("MinInterval").floatValue = 0f;
            entry.FindPropertyRelative("IntervalScope").enumValueIndex = (int)IntervalScope.Entity;
            entry.FindPropertyRelative("BgmFadeDuration").floatValue = -1f;
            entry.FindPropertyRelative("LoopEndRule").stringValue = "";
        }
        ApplyAssetChanges(data);
    }

    private void DrawMonsterProfiles(SerializedObject catalog)
    {
        EditorGUILayout.HelpBox("각 몬스터 항목의 음원·볼륨·피치·지연을 따로 조절합니다. 다른 몬스터를 추가할 때는 기존 항목을 복제하고 몬스터 데이터를 선택하세요.", MessageType.Info);
        SerializedProperty profiles = catalog.FindProperty("Monsters");
        for (int i = 0; i < profiles.arraySize; i++)
        {
            SerializedProperty profile = profiles.GetArrayElementAtIndex(i);
            string name = profile.FindPropertyRelative("DisplayName").stringValue;
            string prefix = profile.FindPropertyRelative("EventPrefix").stringValue;
            string path = "Monsters.Array.data[" + i + "]";
            _previewMonster = profile.FindPropertyRelative("Monster").objectReferenceValue as EnemyData;
            EditorGUILayout.PropertyField(profile.FindPropertyRelative("UseCustomLaunchDeathFade"), new GUIContent(name + " 사망음 페이드 개별 설정"));
            if (profile.FindPropertyRelative("UseCustomLaunchDeathFade").boolValue)
            {
                EditorGUILayout.PropertyField(profile.FindPropertyRelative("LaunchDeathFadeStart"), new GUIContent("페이드 시작 시점 (재생 후 초)"));
                EditorGUILayout.PropertyField(profile.FindPropertyRelative("LaunchDeathFadeDuration"), new GUIContent("페이드 지속 시간 (초)"));
            }
            ApplyAssetChanges(catalog);
            DrawCategory(catalog, path + ".Sounds", string.IsNullOrEmpty(name) ? "몬스터 " + (i + 1) : name, prefix);
            _previewMonster = null;
        }
    }

    private void DuplicateMonster(SerializedObject catalog, string profilePath, SoundSetSO source)
    {
        SerializedProperty original = catalog.FindProperty(profilePath);
        string prefix = original.FindPropertyRelative("EventPrefix").stringValue;
        string label = original.FindPropertyRelative("DisplayName").stringValue + " (복제)";
        SoundSetSO copy = Instantiate(source);
        copy.name = source.name + "_Copy";
        var copiedData = new SerializedObject(copy);
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        string uniquePrefix = "Monster_" + Guid.NewGuid().ToString("N");
        SerializedProperty sounds = copiedData.FindProperty("_sounds");
        for (int i = 0; i < sounds.arraySize; i++)
        {
            SerializedProperty id = sounds.GetArrayElementAtIndex(i).FindPropertyRelative("SoundId");
            string previous = id.stringValue;
            string replacement = uniquePrefix + "." + i;
            if (!string.IsNullOrWhiteSpace(previous)) ids[previous] = replacement;
            id.stringValue = replacement;
        }
        SerializedProperty events = copiedData.FindProperty("_events");
        for (int i = 0; i < events.arraySize; i++)
        {
            SerializedProperty id = events.GetArrayElementAtIndex(i).FindPropertyRelative("SoundId");
            if (ids.TryGetValue(id.stringValue, out string replacement)) id.stringValue = replacement;
        }
        copiedData.ApplyModifiedPropertiesWithoutUndo();
        copiedData.Dispose();
        string assetPath = AssetDatabase.GenerateUniqueAssetPath("Assets/07.ScriptableObjects/Audio/" + copy.name + ".asset");
        AssetDatabase.CreateAsset(copy, assetPath);
        Undo.RegisterCreatedObjectUndo(copy, "Duplicate monster audio");
        SerializedProperty profiles = catalog.FindProperty("Monsters");
        int index = profiles.arraySize;
        profiles.InsertArrayElementAtIndex(index);
        SerializedProperty added = profiles.GetArrayElementAtIndex(index);
        added.FindPropertyRelative("Monster").objectReferenceValue = null;
        added.FindPropertyRelative("DisplayName").stringValue = label;
        added.FindPropertyRelative("EventPrefix").stringValue = prefix;
        added.FindPropertyRelative("Sounds").objectReferenceValue = copy;
        ApplyAssetChanges(catalog);
        AssetDatabase.SaveAssetIfDirty(copy);
        AssetDatabase.SaveAssetIfDirty(_catalog);
    }

    private void DrawGlobalSettings()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("글로벌 설정", EditorStyles.boldLabel);
        var manager = (AudioManager)target;
        float bgm = EditorGUILayout.Slider("BGM 볼륨", manager.GetBgmVolume(), 0f, 1f);
        float sfx = EditorGUILayout.Slider("SFX 볼륨", manager.GetSfxVolume(), 0f, 1f);
        bool bgmMute = EditorGUILayout.Toggle("BGM 음소거", manager.IsBgmMuted());
        bool sfxMute = EditorGUILayout.Toggle("SFX 음소거", manager.IsSfxMuted());
        if (bgm != manager.GetBgmVolume() || sfx != manager.GetSfxVolume() ||
            bgmMute != manager.IsBgmMuted() || sfxMute != manager.IsSfxMuted())
        {
            if (Application.isPlaying)
            {
                Undo.RecordObject(manager, "Change audio volume");
                if (bgm != manager.GetBgmVolume()) manager.SetBgmVolume(bgm);
                if (sfx != manager.GetSfxVolume()) manager.SetSfxVolume(sfx);
                if (bgmMute != manager.IsBgmMuted()) manager.SetBgmMute(bgmMute);
                if (sfxMute != manager.IsSfxMuted()) manager.SetSfxMute(sfxMute);
            }
            else
            {
                serializedObject.FindProperty("_bgmVolume").floatValue = bgm;
                serializedObject.FindProperty("_sfxVolume").floatValue = sfx;
                serializedObject.FindProperty("_isBgmMuted").boolValue = bgmMute;
                serializedObject.FindProperty("_isSfxMuted").boolValue = sfxMute;
                serializedObject.ApplyModifiedProperties();
            }
        }
        if (!Application.isPlaying)
            EditorGUILayout.LabelField("플레이 시작 시 기존 PlayerPrefs 음량 설정을 불러옵니다.", EditorStyles.wordWrappedMiniLabel);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_sfxChannelCount"), new GUIContent("SFX 최대 채널 (시작 시 적용)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_defaultBgmFade"), new GUIContent("기본 BGM 페이드 (초)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_bgmSource"), new GUIContent("BGM 전용 채널"));
        if (Foldout("Legacy", "기존 UI 호환 설정"))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_uiAudioData"));
        serializedObject.ApplyModifiedProperties();
    }

    private static void ApplyAssetChanges(SerializedObject data)
    {
        if (data.ApplyModifiedProperties())
        {
            AssetDatabase.SaveAssetIfDirty(data.targetObject);
        }
    }

    private void SaveDirtyAudioAssets()
    {
        foreach (UnityEngine.Object asset in _assets.Keys)
        {
            if (asset != null) AssetDatabase.SaveAssetIfDirty(asset);
        }
    }

    private SerializedObject AssetObject(UnityEngine.Object asset)
    {
        if (!_assets.TryGetValue(asset, out SerializedObject result))
        {
            result = new SerializedObject(asset);
            _assets.Add(asset, result);
        }
        result.UpdateIfRequiredOrScript();
        return result;
    }

    private bool Foldout(string key, string label, bool initial = false)
    {
        if (!_foldouts.TryGetValue(key, out bool open)) open = initial;
        open = EditorGUILayout.Foldout(open, label, true);
        _foldouts[key] = open;
        return open;
    }

    private void DrawCategory(SerializedObject catalog, string path, string label, string prefix)
    {
        if (!Foldout("Category:" + path, label)) return;
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            SerializedProperty reference = catalog.FindProperty(path);
            bool monsterProfile = path.StartsWith("Monsters.", StringComparison.Ordinal);
            string profilePath = monsterProfile ? path.Substring(0, path.Length - ".Sounds".Length) : null;
            if (monsterProfile)
            {
                SerializedProperty profile = catalog.FindProperty(profilePath);
                EditorGUILayout.PropertyField(profile.FindPropertyRelative("DisplayName"), new GUIContent("몬스터 이름"));
                EditorGUILayout.PropertyField(profile.FindPropertyRelative("Monster"), new GUIContent("몬스터 데이터"));
            }
            EditorGUILayout.PropertyField(reference, new GUIContent("SoundSet"));
            ApplyAssetChanges(catalog);
            var set = reference.objectReferenceValue as SoundSetSO;
            if (set == null) { EditorGUILayout.LabelField("SoundSet을 연결하면 인라인 편집이 표시됩니다."); return; }
            if (monsterProfile && GUILayout.Button("다른 몬스터용으로 복제"))
            {
                DuplicateMonster(catalog, profilePath, set);
                GUIUtility.ExitGUI();
            }
            var data = AssetObject(set);
            SerializedProperty events = data.FindProperty("_events");
            EditorGUILayout.LabelField("이벤트 연결", EditorStyles.boldLabel);
            for (int i = 0; i < events.arraySize; i++)
            {
                SerializedProperty entry = events.GetArrayElementAtIndex(i);
                string eventId = entry.FindPropertyRelative("EventId").stringValue;
                string eventLabel = string.IsNullOrEmpty(eventId) ? "새 이벤트" :
                    AudioEventEditorDefinitions.Description(eventId) + " (" + eventId + ")";
                if (!Foldout($"{path}:{set.GetInstanceID()}:event:{i}", eventLabel)) continue;
                EditorGUI.indentLevel++;
                DrawEvent(data, entry, prefix);
                if (GUILayout.Button("이벤트 삭제"))
                {
                    events.DeleteArrayElementAtIndex(i);
                    ApplyAssetChanges(data);
                    EditorGUI.indentLevel--;
                    break;
                }
                EditorGUI.indentLevel--;
            }
            if (GUILayout.Button("이벤트 추가"))
            {
                int i = events.arraySize;
                events.InsertArrayElementAtIndex(i);
                InitializeEvent(events.GetArrayElementAtIndex(i), prefix);
            }
            ApplyAssetChanges(data);
            if (Foldout(path + ":definitions", "사운드 정의 전체 (미연결 포함)"))
            {
                SerializedProperty sounds = data.FindProperty("_sounds");
                for (int i = 0; i < sounds.arraySize; i++)
                {
                    var sound = sounds.GetArrayElementAtIndex(i);
                    string id = sound.FindPropertyRelative("SoundId").stringValue;
                    if (!Foldout($"{path}:{set.GetInstanceID()}:sound:{i}", string.IsNullOrEmpty(id) ? "새 사운드" : id)) continue;
                    DrawSound(data, sound, set, i);
                    if (GUILayout.Button("사운드 정의 삭제"))
                    {
                        sounds.DeleteArrayElementAtIndex(i);
                        ApplyAssetChanges(data);
                        break;
                    }
                }
                if (GUILayout.Button("사운드 정의 추가"))
                {
                    int i = sounds.arraySize;
                    sounds.InsertArrayElementAtIndex(i);
                    InitializeSound(sounds.GetArrayElementAtIndex(i), prefix);
                }
            }
            ApplyAssetChanges(data);
        }
    }

    private void DrawEvent(SerializedObject data, SerializedProperty entry, string prefix)
    {
        var eventId = entry.FindPropertyRelative("EventId");
        string[] choices = AudioEventEditorDefinitions.Ids(prefix);
        var options = new List<string> { "선택 / 미등록: " + eventId.stringValue };
        options.AddRange(choices);
        int current = Array.IndexOf(choices, eventId.stringValue) + 1;
        int selected = EditorGUILayout.Popup("이벤트 ID", current, options.ToArray());
        if (selected != current && selected > 0) eventId.stringValue = choices[selected - 1];
        Property(entry, "Enabled", "사용");
        string description = AudioEventEditorDefinitions.Description(eventId.stringValue);
        if (selected != current && selected > 0) entry.FindPropertyRelative("TriggerDescription").stringValue = description;
        string savedDescription = entry.FindPropertyRelative("TriggerDescription").stringValue;
        EditorGUILayout.LabelField("기준 이벤트", string.IsNullOrEmpty(savedDescription) ? description : savedDescription, EditorStyles.wordWrappedLabel);
        DrawSoundPicker(entry.FindPropertyRelative("SoundId"));
        Property(entry, "Delay", "재생 지연 (초)");
        Property(entry, "TimeMode", "시간 기준");
        Property(entry, "LifetimePolicy", "수명 정책");
        Property(entry, "Loop", "반복 재생");
        if (entry.FindPropertyRelative("Loop").boolValue) Property(entry, "LoopEndRule", "반복 종료 규칙");
        Property(entry, "MinInterval", "최소 재생 간격 (초)");
        Property(entry, "IntervalScope", "간격 적용 범위");
        string soundId = entry.FindPropertyRelative("SoundId").stringValue;
        if (FindSound(soundId, out SoundSetSO owner, out int index) && owner.Sounds[index].OutputGroup == AudioOutputGroup.Bgm)
            Property(entry, "BgmFadeDuration", "BGM 페이드 (음수: 기본값)");
        ApplyAssetChanges(data);
        if (owner == null) return;
        EditorGUILayout.LabelField("연결된 사운드 정의", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("정의 위치", owner.name);
        List<string> users = SoundReferences(soundId);
        if (users.Count > 1)
            EditorGUILayout.HelpBox("다른 이벤트에서도 사용하는 사운드입니다. 수정은 모든 연결에 반영됩니다.\n" + string.Join("\n", users), MessageType.Info);
        var soundObject = AssetObject(owner);
        DrawSound(soundObject, soundObject.FindProperty("_sounds").GetArrayElementAtIndex(index), owner, index);
        ApplyAssetChanges(soundObject);
    }

    private void DrawSoundPicker(SerializedProperty id)
    {
        var ids = new List<string>();
        foreach (SoundSetSO set in new HashSet<SoundSetSO>(_catalog.EnumerateSets()))
            foreach (SoundData sound in set.Sounds)
                if (sound != null && !string.IsNullOrWhiteSpace(sound.SoundId) && !ids.Contains(sound.SoundId)) ids.Add(sound.SoundId);
        ids.Sort(StringComparer.Ordinal);
        var labels = new List<string> { "미지정 / 미등록: " + id.stringValue };
        labels.AddRange(ids);
        int current = ids.IndexOf(id.stringValue) + 1;
        int selected = EditorGUILayout.Popup("연결 사운드 ID", current, labels.ToArray());
        if (selected != current) id.stringValue = selected == 0 ? string.Empty : ids[selected - 1];
    }

    private bool FindSound(string id, out SoundSetSO owner, out int index)
    {
        foreach (SoundSetSO set in new HashSet<SoundSetSO>(_catalog.EnumerateSets()))
            for (int i = 0; i < set.Sounds.Count; i++)
                if (set.Sounds[i] != null && !string.IsNullOrWhiteSpace(id) && set.Sounds[i].SoundId == id)
                { owner = set; index = i; return true; }
        owner = null; index = -1; return false;
    }

    private List<string> SoundReferences(string id)
    {
        var users = new List<string>();
        foreach (SoundSetSO set in new HashSet<SoundSetSO>(_catalog.EnumerateSets()))
            foreach (EventBinding binding in set.Events)
                if (binding != null && binding.SoundId == id) users.Add(set.name + " / " + binding.EventId);
        return users;
    }

    private void DrawSound(SerializedObject data, SerializedProperty sound, SoundSetSO owner, int index)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            Property(sound, "SoundId", "사운드 ID");
            Property(sound, "DisplayName", "표시 이름");
            SerializedProperty clips = sound.FindPropertyRelative("Clips");
            AudioClip firstClip = clips.arraySize > 0 ? clips.GetArrayElementAtIndex(0).objectReferenceValue as AudioClip : null;
            EditorGUI.BeginChangeCheck();
            var selectedClip = EditorGUILayout.ObjectField("음원", firstClip, typeof(AudioClip), false) as AudioClip;
            if (EditorGUI.EndChangeCheck())
            {
                if (clips.arraySize == 0) clips.InsertArrayElementAtIndex(0);
                clips.GetArrayElementAtIndex(0).objectReferenceValue = selectedClip;
            }
            if (Foldout(owner.GetInstanceID() + ":clips:" + index, "랜덤 재생용 음원 목록"))
                EditorGUILayout.PropertyField(clips, new GUIContent("음원 목록"), true);
            EditorGUILayout.PropertyField(sound.FindPropertyRelative("StartOffset"),
                new GUIContent("앞부분 건너뛰기 (초)", "0.2이면 음원의 0.2초 위치부터 재생합니다. 재생 지연과 별도로 적용되며 원본 파일은 바뀌지 않습니다."));
            Property(sound, "Volume", "항목 볼륨");
            Property(sound, "PitchMin", "피치 최소");
            Property(sound, "PitchMax", "피치 최대");
            Property(sound, "OutputGroup", "출력 그룹");
            Property(sound, "MaxCount", "최대 동시 재생 수");
            Property(sound, "Priority", "우선순위 (클수록 중요)");
            ApplyAssetChanges(data);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("미리듣기")) _previewError = AudioEditorPreview.Play(owner.Sounds[index]);
                if (GUILayout.Button("미리듣기 정지")) { AudioEditorPreview.Stop(); _previewError = null; }
            }
            SoundData previewSound = owner.Sounds[index];
            bool isDeath = (owner.TryGetEvent("Ground.Death", out EventBinding groundDeath) && groundDeath.SoundId == previewSound.SoundId) ||
                (owner.TryGetEvent("Air.Death", out EventBinding airDeath) && airDeath.SoundId == previewSound.SoundId);
            if (isDeath)
            {
                _catalog.GetLaunchDeathFade(_previewMonster, out float start, out float duration);
                EditorGUILayout.LabelField($"날아가는 사망음: 재생 후 {start:0.##}초부터 {duration:0.##}초 동안 페이드");
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("날아가는 사망음 미리듣기"))
                        _previewError = AudioEditorPreview.Play(previewSound, start, duration);
            }
        }
    }

    private static void Property(SerializedProperty parent, string name, string label) =>
        EditorGUILayout.PropertyField(parent.FindPropertyRelative(name), new GUIContent(label));

    private static void InitializeEvent(SerializedProperty entry, string prefix)
    {
        entry.FindPropertyRelative("EventId").stringValue = "";
        entry.FindPropertyRelative("Enabled").boolValue = prefix != "Buff";
        entry.FindPropertyRelative("SoundId").stringValue = "";
        entry.FindPropertyRelative("TriggerDescription").stringValue = "";
        entry.FindPropertyRelative("Delay").floatValue = 0f;
        entry.FindPropertyRelative("TimeMode").enumValueIndex = prefix == "UI" || prefix == "Music" ? 1 : 0;
        entry.FindPropertyRelative("LifetimePolicy").enumValueIndex = prefix == "UI" ? 4 : prefix == "Music" || prefix == "Prologue" ? 3 : 0;
        entry.FindPropertyRelative("Loop").boolValue = false;
        entry.FindPropertyRelative("MinInterval").floatValue = 0f;
        entry.FindPropertyRelative("IntervalScope").enumValueIndex = 0;
        entry.FindPropertyRelative("BgmFadeDuration").floatValue = -1f;
        entry.FindPropertyRelative("LoopEndRule").stringValue = "";
    }

    private static void InitializeSound(SerializedProperty sound, string prefix)
    {
        sound.FindPropertyRelative("SoundId").stringValue = "";
        sound.FindPropertyRelative("DisplayName").stringValue = "";
        sound.FindPropertyRelative("Clips").ClearArray();
        sound.FindPropertyRelative("StartOffset").floatValue = 0f;
        sound.FindPropertyRelative("Volume").floatValue = 1f;
        sound.FindPropertyRelative("PitchMin").floatValue = 1f;
        sound.FindPropertyRelative("PitchMax").floatValue = 1f;
        sound.FindPropertyRelative("OutputGroup").enumValueIndex = prefix == "Music" ? 1 : 0;
        sound.FindPropertyRelative("Priority").intValue = prefix == "UI" ? 70 : 50;
        sound.FindPropertyRelative("MaxCount").intValue = prefix == "Music" ? 1 : 4;
    }
}
