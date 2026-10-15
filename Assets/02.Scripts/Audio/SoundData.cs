using System;
using UnityEngine;

namespace GameAudio
{
    [Serializable]
    public sealed class SoundData
    {
        public string SoundId;
        public string DisplayName;
        public AudioClip[] Clips = Array.Empty<AudioClip>();
        [Tooltip("음원 앞부분을 이 시간만큼 건너뛰고 재생합니다. 원본 파일은 변경하지 않습니다.")]
        [Min(0f)] public float StartOffset;
        [Range(0f, 1f)] public float Volume = 1f;
        [Tooltip("음원 샘플 증폭 배율. 1이면 원래 크기입니다.")]
        [Range(0f, 3f)] public float Gain = 1f;
        [Min(0.01f)] public float PitchMin = 1f;
        [Min(0.01f)] public float PitchMax = 1f;
        public AudioOutputGroup OutputGroup = AudioOutputGroup.Sfx;
        [Range(0, 100)] public int Priority = 50;
        [Min(1)] public int MaxCount = 4;

        public bool HasValidStartOffset => !float.IsNaN(StartOffset) && !float.IsInfinity(StartOffset) && StartOffset >= 0f;

        public bool TryGetStartSample(AudioClip clip, out int sample)
        {
            sample = 0;
            if (clip == null || !HasValidStartOffset || clip.frequency <= 0 || clip.samples <= 0)
                return false;
            double position = Math.Floor((double)StartOffset * clip.frequency);
            if (position >= clip.samples) return false;
            sample = (int)position;
            return true;
        }

        public bool HasValidClip
        {
            get
            {
                if (Clips == null) return false;
                foreach (AudioClip clip in Clips) if (clip != null) return true;
                return false;
            }
        }
    }
}
