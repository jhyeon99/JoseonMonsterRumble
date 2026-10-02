using UnityEngine;

[CreateAssetMenu(fileName = "SO_SoulOrbSettings", menuName = "UI/Soul Orb Settings")]
public class UI_SoulOrbSettings : ScriptableObject
{
    [Header("리소스")]
    [SerializeField] private Sprite _emptySprite;
    [SerializeField] private Sprite _filledSprite;
    [SerializeField] private Sprite _fullChargePreviewSprite;
    [SerializeField] private RuntimeAnimatorController _animatorController;

    [Header("크기")]
    [SerializeField] private Vector3 _baseOrbScale = Vector3.one;
    [SerializeField] private Vector3 _fullChargeScale = Vector3.one;
    [SerializeField] private Vector3 _consumeScale = Vector3.one;

    [Header("획득 피드백")]
    [SerializeField, Min(0f)] private float _gainDuration = 0.18f;
    [SerializeField] private Color _flashColor = new Color(0.65f, 0.9f, 1f, 1f);
    [SerializeField] private AnimationCurve _gainScaleCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
    [SerializeField] private AnimationCurve _gainFlashCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Header("소비 피드백")]
    [SerializeField, Min(0f)] private float _consumeDuration = 0.5f;
    [SerializeField] private AnimationCurve _consumeScaleCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
    [SerializeField] private AnimationCurve _consumeFlashCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Header("Animator 상태")]
    [SerializeField] private string _fullChargeStateName = "Base Layer.Soul_FullCharge";
    [SerializeField] private string _consumeStateName = "Base Layer.Soul_Consume";

    public Sprite EmptySprite => _emptySprite;
    public Sprite FilledSprite => _filledSprite;
    public Sprite FullChargePreviewSprite => _fullChargePreviewSprite;
    public RuntimeAnimatorController AnimatorController => _animatorController;
    public Vector3 BaseOrbScale => _baseOrbScale;
    public Vector3 FullChargeScale => _fullChargeScale;
    public Vector3 ConsumeScale => _consumeScale;
    public float GainDuration => _gainDuration;
    public Color FlashColor => _flashColor;
    public float ConsumeDuration => _consumeDuration;
    public int FullChargeStateHash => Animator.StringToHash(_fullChargeStateName);
    public int ConsumeStateHash => Animator.StringToHash(_consumeStateName);

    public float CalculateGainScale(float progress)
    {
        return CalculateCurveValue(_gainScaleCurve, progress, 1f);
    }

    public float CalculateGainFlash(float progress)
    {
        return CalculateCurveValue(_gainFlashCurve, progress, 0f);
    }

    public float CalculateConsumeScale(float progress)
    {
        return CalculateCurveValue(_consumeScaleCurve, progress, 1f);
    }

    public float CalculateConsumeFlash(float progress)
    {
        return CalculateCurveValue(_consumeFlashCurve, progress, 0f);
    }

    public bool HasRequiredReferences()
    {
        return _emptySprite != null && _filledSprite != null && _fullChargePreviewSprite != null &&
            _animatorController != null;
    }

    private float CalculateCurveValue(AnimationCurve curve, float progress, float fallbackValue)
    {
        return curve == null ? fallbackValue : curve.Evaluate(Mathf.Clamp01(progress));
    }
}
