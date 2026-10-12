using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameAudio
{
    public static class AudioLifetimeRegistry
    {
        private static uint _nextScene, _nextOwner;
        private static readonly Dictionary<int, AudioSceneKey> Scenes = new Dictionary<int, AudioSceneKey>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Scenes.Clear();
            // Domain Reload를 끈 반복 Play에서도 이전 세대와 겹치지 않도록 카운터는 유지한다.
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        public static uint NextOwnerGeneration()
        {
            if (++_nextOwner == 0) ++_nextOwner;
            return _nextOwner;
        }

        public static AudioSceneKey ForScene(Scene scene)
        {
            if (!scene.IsValid()) return default;
            if (!Scenes.TryGetValue(scene.handle, out AudioSceneKey key))
            {
                if (++_nextScene == 0) ++_nextScene;
                Scenes[scene.handle] = key = new AudioSceneKey(scene.handle, _nextScene);
            }
            return key;
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            if (!Scenes.TryGetValue(scene.handle, out AudioSceneKey key)) return;
            if (AudioManager.ExistingInstance != null) AudioManager.ExistingInstance.ReleaseScene(key);
            Scenes.Remove(scene.handle);
        }
    }
}
