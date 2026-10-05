using UnityEngine;

public class UI_SoulGaugeBinder : MonoBehaviour
{
    [SerializeField] private PlayerSoul _soulSourceComponent;
    [SerializeField] private UI_SoulGauge _soulGauge;

    private ISoulSource _soulSource;

    private void OnEnable()
    {
        if (!TryResolveReferences())
        {
            return;
        }

        _soulSource.OnSoulChanged += HandleSoulChanged;
        _soulGauge.SetSoulImmediately(_soulSource.CurrentSoul, _soulSource.MaxSoul);
    }

    private void OnDisable()
    {
        if (_soulSource != null)
        {
            _soulSource.OnSoulChanged -= HandleSoulChanged;
        }
    }

    private void HandleSoulChanged(float currentSoul, float maxSoul)
    {
        _soulGauge.SetSoul(currentSoul, maxSoul);
    }

    private bool TryResolveReferences()
    {
        if (_soulGauge == null)
        {
            Debug.LogWarning($"[{nameof(UI_SoulGaugeBinder)}] Soul Gauge 참조가 필요합니다.", this);
            return false;
        }

        if (_soulSourceComponent == null)
        {
            _soulSourceComponent = FindFirstObjectByType<PlayerSoul>();
        }

        _soulSource = _soulSourceComponent as ISoulSource;
        if (_soulSource == null)
        {
            Debug.LogWarning($"[{nameof(UI_SoulGaugeBinder)}] Soul Source는 {nameof(ISoulSource)}를 구현해야 합니다.", this);
            return false;
        }

        return true;
    }
}