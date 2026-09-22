using UnityEngine;

[CreateAssetMenu(fileName = "AttackDataTableSO", menuName = "Scriptable Objects/AttackDataTableSO")]
public class AttackDataTableSO : ScriptableObject
{
    public AttackData[] Data;
    public float ComboResetTime = 1.0f;
}