using UnityEngine;

public enum DokkebibulBuffType
{
    Red_Attack,
    Green_Heal,
    Blue_GuardBreak,
    Yellow_Shield
}

[CreateAssetMenu(fileName = "SO_Buff_New", menuName = "Enemy/Buff Type Data")]
public class SO_BuffTypeData : ScriptableObject
{
    [Header("버프 기본 식별")]
    [SerializeField] private DokkebibulBuffType _buffType;
    public DokkebibulBuffType BuffType => _buffType;

    [Header("시각 색상 팔레트")]
    [SerializeField] private Color _flameColor = Color.white;
    [SerializeField] private Color _linkStartColor = new Color(0.2f, 0.8f, 1f, 0.9f);
    [SerializeField] private Color _linkEndColor = new Color(0.1f, 0.5f, 1f, 0.8f);
    [SerializeField] private Color _targetAuraColor = new Color(0.35f, 0.85f, 1f, 1f);

    public Color FlameColor => _flameColor;
    public Color LinkStartColor => _linkStartColor;
    public Color LinkEndColor => _linkEndColor;
    public Color TargetAuraColor => _targetAuraColor;

    [Header("적염 (빨강) - 공격력 배율")]
    [SerializeField, Min(1f)] private float _attackDamageMultiplier = 2.0f;
    public float AttackDamageMultiplier => _attackDamageMultiplier;

    [Header("녹염 (초록) - 초당 체력 회복 비율 (0.05 = 5%)")]
    [SerializeField, Range(0.01f, 0.5f)] private float _healPercentPerSecond = 0.05f;
    public float HealPercentPerSecond => _healPercentPerSecond;

    [Header("청염 (파랑) - 추가 가드 게이지 소모량")]
    [SerializeField, Min(0f)] private float _extraGuardGaugeCost = 45f;
    public float ExtraGuardGaugeCost => _extraGuardGaugeCost;

    [Header("황염 (노랑) - 대상 최대 체력 비례 보호막 비율 (0.4 = 40%)")]
    [SerializeField, Range(0.1f, 2.0f)] private float _shieldRatio = 0.4f;
    public float ShieldRatio => _shieldRatio;
}
