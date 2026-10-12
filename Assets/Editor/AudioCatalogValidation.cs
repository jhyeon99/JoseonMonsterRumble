using System;
using System.Collections.Generic;
using GameAudio;

public static class AudioCatalogValidation
{
    public static List<string> Validate(AudioCatalogSO catalog)
    {
        var issues = new List<string>();
        if (catalog == null) { issues.Add("AudioCatalog가 지정되지 않았습니다."); return issues; }
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
