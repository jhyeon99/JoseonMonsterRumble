using System;
using System.Reflection;
using GameAudio;
using UnityEditor;
using UnityEngine;

// AudioUtil은 Unity 내부 API이므로 reflection으로 버전 차이와 미지원 상태를 처리한다.
[InitializeOnLoad]
internal static class AudioEditorPreview
{
    private static AudioClip _previewClip;
    private static readonly System.Random Random = new System.Random();
    private const float PreviewSeconds = 15f;

    static AudioEditorPreview()
    {
        AssemblyReloadEvents.beforeAssemblyReload += Stop;
        EditorApplication.quitting += Stop;
        EditorApplication.playModeStateChanged += state => Stop();
    }

    public static string Play(SoundData sound, float fadeStart = -1f, float fadeDuration = 0f)
    {
        Stop();
        if (EditorApplication.isPlayingOrWillChangePlaymode) return "미리듣기는 Edit Mode에서 사용하세요.";
        if (sound == null || !sound.HasValidClip || sound.PitchMin <= 0f || sound.PitchMax < sound.PitchMin ||
            float.IsNaN(sound.PitchMin) || float.IsInfinity(sound.PitchMin) || float.IsNaN(sound.PitchMax) || float.IsInfinity(sound.PitchMax) || float.IsNaN(sound.Volume) || float.IsInfinity(sound.Volume)) return "클립과 피치 범위를 확인하세요.";
        if (float.IsNaN(fadeStart) || float.IsInfinity(fadeStart) || float.IsNaN(fadeDuration) || float.IsInfinity(fadeDuration))
            return "페이드 시간을 확인하세요.";
        fadeDuration = Mathf.Max(0f, fadeDuration);
        try
        {
            Type audioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            MethodInfo play = audioUtil?.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            if (play == null) return "이 Unity 버전의 AudioUtil 미리듣기 API를 찾지 못했습니다.";
            int count = 0;
            foreach (AudioClip clip in sound.Clips) if (sound.TryGetStartSample(clip, out _)) count++;
            if (count == 0) return "앞부분 건너뛰기 시간이 음원 길이보다 짧아야 합니다.";
            int choice = Random.Next(count);
            AudioClip source = null;
            foreach (AudioClip clip in sound.Clips) if (sound.TryGetStartSample(clip, out _) && choice-- == 0) { source = clip; break; }
            if (source.loadState != AudioDataLoadState.Loaded && !source.LoadAudioData()) return "음원 데이터를 로드하지 못했습니다.";
            if (!sound.TryGetStartSample(source, out int startSample))
                return "앞부분 건너뛰기 시간은 0 이상이고 음원 길이보다 짧아야 합니다.";
            int remainingFrames = source.samples - startSample;
            float pitch = Mathf.Lerp(sound.PitchMin, sound.PitchMax, (float)Random.NextDouble());
            float previewDuration = fadeStart >= 0f ? Mathf.Min(PreviewSeconds, fadeStart + fadeDuration) : PreviewSeconds;
            int outputFrames = Mathf.Max(1, Mathf.CeilToInt(Mathf.Min(remainingFrames / pitch, source.frequency * previewDuration)));
            int inputFrames = Mathf.CeilToInt(Mathf.Min(remainingFrames, outputFrames * pitch + 1f));
            var input = new float[inputFrames * source.channels];
            if (!source.GetData(input, startSample)) return "음원을 읽을 수 없습니다. 미리듣기 시 Import Load Type을 Decompress On Load로 설정하세요.";
            var output = new float[outputFrames * source.channels];
            for (int frame = 0; frame < outputFrames; frame++)
            {
                float envelope = 1f;
                float elapsed = (float)frame / source.frequency;
                if (fadeStart >= 0f && elapsed >= fadeStart)
                {
                    float remaining = fadeDuration > 0f ? Mathf.Clamp01(1f - (elapsed - fadeStart) / fadeDuration) : 0f;
                    envelope = remaining * remaining;
                }
                float position = Mathf.Min(frame * pitch, inputFrames - 1);
                int left = Mathf.FloorToInt(position);
                int right = Mathf.Min(left + 1, inputFrames - 1);
                for (int channel = 0; channel < source.channels; channel++)
                    output[frame * source.channels + channel] = Mathf.Lerp(input[left * source.channels + channel],
                        input[right * source.channels + channel], position - left) * Mathf.Clamp01(sound.Volume) * Mathf.Clamp(sound.Gain, 0f, 3f) * envelope;
            }
            // AudioUtil에는 항목 볼륨/피치 인자가 없으므로 임시 PCM에만 적용한다. 원본 에셋은 수정하지 않는다.
            _previewClip = AudioClip.Create("Audio Inspector Preview", outputFrames, source.channels, source.frequency, false);
            _previewClip.hideFlags = HideFlags.HideAndDontSave;
            _previewClip.SetData(output, 0);
            play.Invoke(null, new object[] { _previewClip, 0, false });
            return null;
        }
        catch (Exception exception)
        {
            Stop();
            return "미리듣기 실패: " + (exception.InnerException?.Message ?? exception.Message);
        }
    }

    public static void Stop()
    {
        if (_previewClip == null) return;
        try
        {
            Type audioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            audioUtil?.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null)?.Invoke(null, null);
        }
        catch (Exception exception) { Debug.LogWarning("오디오 미리듣기 정지 실패: " + exception.Message); }
        finally
        {
            UnityEngine.Object.DestroyImmediate(_previewClip);
            _previewClip = null;
        }
    }
}
