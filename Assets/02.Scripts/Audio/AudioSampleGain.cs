using System.Threading;
using UnityEngine;

namespace GameAudio
{
    [DisallowMultipleComponent]
    public sealed class AudioSampleGain : MonoBehaviour
    {
        private float _gain = 1f;

        public void SetGain(float gain)
        {
            float safe = float.IsNaN(gain) || float.IsInfinity(gain) ? 1f : Mathf.Clamp(gain, 0f, 3f);
            Volatile.Write(ref _gain, safe);
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            float gain = Volatile.Read(ref _gain);
            if (gain == 1f) return;
            for (int i = 0; i < data.Length; i++) data[i] *= gain;
        }
    }
}
