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
        [Range(0f, 1f)] public float Volume = 1f;
        [Min(0.01f)] public float PitchMin = 1f;
        [Min(0.01f)] public float PitchMax = 1f;
        public AudioOutputGroup OutputGroup = AudioOutputGroup.Sfx;
        [Range(0, 100)] public int Priority = 50;
        [Min(1)] public int MaxCount = 4;

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
