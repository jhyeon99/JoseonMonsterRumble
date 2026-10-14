using UnityEngine;

public class SpikeOff : MonoBehaviour
{
    [SerializeField] private DokkaebiPlatformWavePreview _wavePreview;

    private void Update()
    {
        if (_wavePreview != null && _wavePreview.IsCompleted)
        {
            gameObject.SetActive(false);
        }
    }
}
