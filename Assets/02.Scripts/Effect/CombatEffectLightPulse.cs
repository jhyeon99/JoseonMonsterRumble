using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatEffectLightPulse : MonoBehaviour
{
    private void OnEnable()
    {
        BossCombatLightController.PlayStompPulse(transform.position);
    }
}
