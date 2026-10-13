using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameAudio
{
    [Serializable]
    public sealed class EnemySoundSets
    {
        public SoundSetSO Melee;
        public SoundSetSO Ranged;
    }

    [Serializable]
    public sealed class MonsterSoundProfile
    {
        public EnemyData Monster;
        public string DisplayName;
        public string EventPrefix = "Ground";
        public SoundSetSO Sounds;
    }

    [CreateAssetMenu(menuName = "Audio/Audio Catalog", fileName = "SO_AudioCatalog")]
    public sealed class AudioCatalogSO : ScriptableObject
    {
        public SoundSetSO Player;
        public List<MonsterSoundProfile> Monsters = new List<MonsterSoundProfile>();
        public EnemySoundSets Ground = new EnemySoundSets();
        public EnemySoundSets Air = new EnemySoundSets();
        public SoundSetSO Boss;
        public SoundSetSO Music;
        public SoundSetSO Item;
        public SoundSetSO Environment;
        public SoundSetSO UI;
        public SoundSetSO Prologue;
        [Tooltip("본 스테이지 사용이 확정될 때 연결하는 조건부 데이터입니다.")]
        public SoundSetSO Buff;

        public bool TryGetMonsterSet(EnemyData monster, out SoundSetSO set)
        {
            if (monster != null && Monsters != null)
            {
                foreach (MonsterSoundProfile profile in Monsters)
                {
                    if (profile != null && profile.Monster == monster)
                    {
                        set = profile.Sounds;
                        return true;
                    }
                }
            }
            set = null;
            return false;
        }

        public SoundSetSO GetSet(string category)
        {
            switch (category)
            {
                case "Player": return Player;
                case "Boss": return Boss;
                case "Music": return Music;
                case "Item": return Item;
                case "Environment": return Environment;
                case "UI": return UI;
                case "Prologue": return Prologue;
                case "Buff": return Buff;
                default: return null;
            }
        }

        public IEnumerable<SoundSetSO> EnumerateSets()
        {
            if (Player != null) yield return Player;
            if (Monsters != null)
            {
                foreach (MonsterSoundProfile profile in Monsters)
                {
                    if (profile?.Sounds != null) yield return profile.Sounds;
                }
            }
            if (Ground?.Melee != null) yield return Ground.Melee;
            if (Ground?.Ranged != null) yield return Ground.Ranged;
            if (Air?.Melee != null) yield return Air.Melee;
            if (Air?.Ranged != null) yield return Air.Ranged;
            if (Boss != null) yield return Boss;
            if (Music != null) yield return Music;
            if (Item != null) yield return Item;
            if (Environment != null) yield return Environment;
            if (UI != null) yield return UI;
            if (Prologue != null) yield return Prologue;
            if (Buff != null) yield return Buff;
        }

        // 모든 분류에서 조회하므로 다른 SoundSet에 정의된 공유 사운드도 참조할 수 있다.
        // ID 중복 검사와 런타임 조회 캐시는 이후 매니저/인스펙터에서 구축한다.
        public bool TryGetSound(string soundId, out SoundData sound)
        {
            foreach (SoundSetSO set in EnumerateSets())
                if (set.TryGetSound(soundId, out sound)) return true;
            sound = null;
            return false;
        }
    }
}
