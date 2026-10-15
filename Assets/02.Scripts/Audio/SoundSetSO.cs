using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameAudio
{
    [CreateAssetMenu(menuName = "Audio/Sound Set", fileName = "SO_SoundSet")]
    public sealed class SoundSetSO : ScriptableObject
    {
        [SerializeField] private List<SoundData> _sounds = new List<SoundData>();
        [SerializeField] private List<EventBinding> _events = new List<EventBinding>();
        public IReadOnlyList<SoundData> Sounds => _sounds;
        public IReadOnlyList<EventBinding> Events => _events;

        public bool TryGetEvent(string eventId, out EventBinding binding)
        {
            binding = null;
            if (string.IsNullOrWhiteSpace(eventId) || _events == null) return false;
            foreach (EventBinding candidate in _events)
            {
                if (candidate != null && string.Equals(candidate.EventId, eventId, StringComparison.Ordinal))
                {
                    binding = candidate;
                    return true;
                }
            }
            return false;
        }

        public bool TryGetSound(string soundId, out SoundData sound)
        {
            sound = null;
            if (string.IsNullOrWhiteSpace(soundId) || _sounds == null) return false;
            foreach (SoundData candidate in _sounds)
            {
                if (candidate != null && string.Equals(candidate.SoundId, soundId, StringComparison.Ordinal))
                {
                    sound = candidate;
                    return true;
                }
            }
            return false;
        }
    }
}
