using UnityEngine;

public class UI_GuardGaugeBinder : MonoBehaviour
{
    [SerializeField] private PlayerGuard _guardSource;
    [SerializeField] private UI_GuardGauge _guardGauge;

    private void OnEnable()
    {
        if (!TryResolveReferences())
        {
            return;
        }

        _guardSource.OnGaugeChanged -= HandleGaugeChanged;
        _guardSource.OnGaugeChanged += HandleGaugeChanged;
        _guardSource.OnGuardBroken -= HandleGuardBroken;
        _guardSource.OnGuardBroken += HandleGuardBroken;

        _guardGauge.SetGauge(_guardSource.CurrentGauge, _guardSource.MaxGauge);
    }

    private void OnDisable()
    {
        if (_guardSource == null)
        {
            return;
        }

        _guardSource.OnGaugeChanged -= HandleGaugeChanged;
        _guardSource.OnGuardBroken -= HandleGuardBroken;
    }

    private void HandleGaugeChanged(float currentGauge, float maxGauge)
    {
        _guardGauge.SetGauge(currentGauge, maxGauge);
    }

    private void HandleGuardBroken()
    {
        _guardGauge.PlayBreakFeedback();
    }

    private bool TryResolveReferences()
    {
        if (_guardGauge == null)
        {
            Debug.LogWarning($"[{nameof(UI_GuardGaugeBinder)}] Guard Gauge 참조가 필요합니다.", this);
            return false;
        }

        if (_guardSource == null)
        {
            _guardSource = FindFirstObjectByType<PlayerGuard>();
        }

        if (_guardSource == null)
        {
            Debug.LogWarning($"[{nameof(UI_GuardGaugeBinder)}] Player Guard를 찾을 수 없습니다.", this);
            return false;
        }

        return true;
    }
}
