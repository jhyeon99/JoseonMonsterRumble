using System;
using System.Collections.Generic;
using GameAudio;

public static class AudioCatalogValidation
{
    public static List<string> Validate(AudioCatalogSO catalog)
    {
        var issues = new List<string>();
        if (catalog == null) { issues.Add("AudioCatalog가 지정되지 않았습니다."); return issues; }
        var monsters = new HashSet<EnemyData>();
        var monsterSets = new HashSet<SoundSetSO>();
        if (catalog.Monsters != null)
        {
            foreach (MonsterSoundProfile profile in catalog.Monsters)
            {
                if (profile == null) { issues.Add("몬스터 오디오 항목이 비어 있습니다."); continue; }
                if (profile.Monster == null) issues.Add(profile.DisplayName + ": 몬스터 데이터를 선택하세요.");
                else if (!monsters.Add(profile.Monster)) issues.Add(profile.DisplayName + ": 같은 몬스터 데이터가 중복 등록되었습니다.");
                if (profile.Sounds == null) issues.Add(profile.DisplayName + ": SoundSet을 연결하세요.");
                else if (!monsterSets.Add(profile.Sounds)) issues.Add(profile.DisplayName + ": 다른 몬스터와 SoundSet을 공유하고 있습니다. 개별 음원이 필요하면 복제하세요.");
            }
        }
        var sets = new HashSet<SoundSetSO>(catalog.EnumerateSets());
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (SoundSetSO set in sets)
        {
            if (set.Sounds == null || set.Events == null) { issues.Add($"{set.name}: 목록이 비어 있습니다."); continue; }
            foreach (SoundData sound in set.Sounds)
            {
                if (sound == null) { issues.Add($"{set.name}: 비어 있는 사운드 정의가 있습니다."); continue; }
                string label = $"{set.name} / {sound.SoundId}";
                if (string.IsNullOrWhiteSpace(sound.SoundId)) issues.Add($"{set.name}: 사운드 ID가 비어 있습니다.");
                else if (ids.TryGetValue(sound.SoundId, out string first)) issues.Add($"사운드 ID 중복: {sound.SoundId} ({first}, {set.name})");
                else ids.Add(sound.SoundId, set.name);
                if (!sound.HasValidClip) issues.Add($"{label}: 유효한 클립이 없습니다.");
                else if (Array.Exists(sound.Clips, clip => clip == null)) issues.Add($"{label}: 음원 목록에 미지정 클립이 있습니다.");
                if (!Finite(sound.PitchMin) || !Finite(sound.PitchMax) || sound.PitchMin <= 0f || sound.PitchMax < sound.PitchMin)
                    issues.Add($"{label}: 피치는 양수이며 최소 ≤ 최대여야 합니다.");
                if (!Finite(sound.Volume) || sound.Volume < 0f || sound.Volume > 1f) issues.Add($"{label}: 볼륨 범위는 0~1입니다.");
                if (!sound.HasValidStartOffset) issues.Add($"{label}: 앞부분 건너뛰기 시간은 유한한 0 이상의 값이어야 합니다.");
                else if (sound.Clips != null)
                {
                    foreach (UnityEngine.AudioClip clip in sound.Clips)
                    {
                        if (clip != null && !sound.TryGetStartSample(clip, out _))
                            issues.Add($"{label}: 앞부분 건너뛰기 시간이 {clip.name}의 길이 이상입니다. 이 음원은 재생되지 않습니다.");
                    }
                }
                if (sound.MaxCount < 1) issues.Add($"{label}: 최대 동시 재생 수는 1 이상이어야 합니다.");
            }
        }
        foreach (SoundSetSO set in sets)
        {
            if (set.Events == null) continue;
            var eventIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (EventBinding binding in set.Events)
            {
                if (binding == null) { issues.Add($"{set.name}: 비어 있는 이벤트 연결이 있습니다."); continue; }
                string label = $"{set.name} / {binding.EventId}";
                if (string.IsNullOrWhiteSpace(binding.EventId)) issues.Add($"{set.name}: 이벤트 ID가 비어 있습니다.");
                else if (!eventIds.Add(binding.EventId)) issues.Add($"{label}: 같은 SoundSet의 이벤트 ID가 중복됩니다.");
                if (string.IsNullOrWhiteSpace(binding.SoundId) || !ids.ContainsKey(binding.SoundId))
                    issues.Add($"{label}: 연결 사운드 ID가 없거나 등록되지 않았습니다 ({binding.SoundId}).");
                if (!Finite(binding.Delay) || binding.Delay < 0f || !Finite(binding.MinInterval) || binding.MinInterval < 0f)
                    issues.Add($"{label}: 지연/최소 간격은 0 이상이어야 합니다.");
                if (!binding.HasValidLifetime) issues.Add($"{label}: 반복음은 Result 수명을 사용할 수 없고 종료 규칙이 필요합니다.");
            }
        }
        return issues;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
