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
            DrawCategory(catalogObject, "Ground.Melee", "지상 / 근접", "Ground");
            DrawCategory(catalogObject, "Ground.Ranged", "지상 / 원거리", "Ground");
            DrawCategory(catalogObject, "Air.Melee", "공중 / 근접", "Air");
            DrawCategory(catalogObject, "Air.Ranged", "공중 / 원거리", "Air");
            DrawCategory(catalogObject, "Buff", "버프형 (조건부)", "Buff");
            EditorGUI.indentLevel--;
        }
        DrawCategory(catalogObject, "Boss", "보스", "Boss");
        DrawCategory(catalogObject, "Music", "BGM", "Music");
        DrawCategory(catalogObject, "Item", "아이템", "Item");
        DrawCategory(catalogObject, "Environment", "환경", "Environment");
        DrawCategory(catalogObject, "UI", "UI", "UI");
        DrawCategory(catalogObject, "Prologue", "프롤로그", "Prologue");
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
            EditorGUILayout.PropertyField(reference, new GUIContent("SoundSet"));
            catalog.ApplyModifiedProperties();
            var set = reference.objectReferenceValue as SoundSetSO;
            if (set == null) { EditorGUILayout.LabelField("SoundSet을 연결하면 인라인 편집이 표시됩니다."); return; }
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
                    data.ApplyModifiedProperties();
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
            data.ApplyModifiedProperties();
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
                        data.ApplyModifiedProperties();
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
            data.ApplyModifiedProperties();
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
        data.ApplyModifiedProperties();
        if (owner == null) return;
        EditorGUILayout.LabelField("연결된 사운드 정의", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("정의 위치", owner.name);
        List<string> users = SoundReferences(soundId);
        if (users.Count > 1)
            EditorGUILayout.HelpBox("다른 이벤트에서도 사용하는 사운드입니다. 수정은 모든 연결에 반영됩니다.\n" + string.Join("\n", users), MessageType.Info);
        var soundObject = AssetObject(owner);
        DrawSound(soundObject, soundObject.FindProperty("_sounds").GetArrayElementAtIndex(index), owner, index);
        soundObject.ApplyModifiedProperties();
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
            Property(sound, "Volume", "항목 볼륨");
            Property(sound, "PitchMin", "피치 최소");
            Property(sound, "PitchMax", "피치 최대");
            Property(sound, "OutputGroup", "출력 그룹");
            Property(sound, "MaxCount", "최대 동시 재생 수");
            Property(sound, "Priority", "우선순위 (클수록 중요)");
            data.ApplyModifiedProperties();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("미리듣기")) _previewError = AudioEditorPreview.Play(owner.Sounds[index]);
                if (GUILayout.Button("미리듣기 정지")) { AudioEditorPreview.Stop(); _previewError = null; }
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
        sound.FindPropertyRelative("Volume").floatValue = 1f;
        sound.FindPropertyRelative("PitchMin").floatValue = 1f;
        sound.FindPropertyRelative("PitchMax").floatValue = 1f;
        sound.FindPropertyRelative("OutputGroup").enumValueIndex = prefix == "Music" ? 1 : 0;
        sound.FindPropertyRelative("Priority").intValue = prefix == "UI" ? 70 : 50;
        sound.FindPropertyRelative("MaxCount").intValue = prefix == "Music" ? 1 : 4;
    }
}
