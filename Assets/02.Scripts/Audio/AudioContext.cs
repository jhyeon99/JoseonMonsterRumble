using System;
using UnityEngine;

namespace GameAudio
{
    /// <summary>개체 ID와 활성화 세대. 재활성화 때 Generation을 갱신한다. 0은 미지정이다.</summary>
    public readonly struct AudioOwnerKey : IEquatable<AudioOwnerKey>
    {
        public int OwnerId { get; }
        public uint Generation { get; }
        public bool IsEmpty => OwnerId == 0 || Generation == 0;
        public AudioOwnerKey(int ownerId, uint generation) { OwnerId = ownerId; Generation = generation; }
        public bool Equals(AudioOwnerKey other) => OwnerId == other.OwnerId && Generation == other.Generation;
        public override bool Equals(object obj) => obj is AudioOwnerKey other && Equals(other);
        public override int GetHashCode() => unchecked(OwnerId * 397 ^ Generation.GetHashCode());
        public static bool operator ==(AudioOwnerKey left, AudioOwnerKey right) => left.Equals(right);
        public static bool operator !=(AudioOwnerKey left, AudioOwnerKey right) => !left.Equals(right);
    }

    /// <summary>씬 handle과 로드 세대. 같은 씬 재로드 시에도 Generation을 갱신한다.</summary>
    public readonly struct AudioSceneKey : IEquatable<AudioSceneKey>
    {
        public int SceneHandle { get; }
        public uint Generation { get; }
        public bool IsEmpty => Generation == 0;
        public AudioSceneKey(int sceneHandle, uint generation) { SceneHandle = sceneHandle; Generation = generation; }
        public bool Equals(AudioSceneKey other) => SceneHandle == other.SceneHandle && Generation == other.Generation;
        public override bool Equals(object obj) => obj is AudioSceneKey other && Equals(other);
        public override int GetHashCode() => unchecked(SceneHandle * 397 ^ Generation.GetHashCode());
        public static bool operator ==(AudioSceneKey left, AudioSceneKey right) => left.Equals(right);
        public static bool operator !=(AudioSceneKey left, AudioSceneKey right) => !left.Equals(right);
    }

    /// <summary>요청 시점의 값만 보관한다. 결과음은 제거된 개체의 Transform에 의존하지 않는다.</summary>
    public readonly struct AudioContext
    {
        public AudioOwnerKey Owner { get; }
        public ulong ActionExecutionId { get; } // 0: 행동과 무관한 요청
        public AudioSceneKey Scene { get; }
        public Vector3 Position { get; }

        public AudioContext(AudioOwnerKey owner, ulong actionExecutionId, AudioSceneKey scene, Vector3 position)
        {
            Owner = owner;
            ActionExecutionId = actionExecutionId;
            Scene = scene;
            Position = position;
        }

        public bool Supports(LifetimePolicy policy)
        {
            switch (policy)
            {
                case LifetimePolicy.Action: return !Owner.IsEmpty && ActionExecutionId != 0 && !Scene.IsEmpty;
                case LifetimePolicy.Entity: return !Owner.IsEmpty && !Scene.IsEmpty;
                case LifetimePolicy.Result:
                case LifetimePolicy.Scene: return !Scene.IsEmpty;
                case LifetimePolicy.Global: return true;
                default: return false;
            }
        }
    }
}
