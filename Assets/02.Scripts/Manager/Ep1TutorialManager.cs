using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum Ep1TutorialHintType
{
    HighJump,
    Respawn,
    Parry,
    Guard,
    TalismanTap,
    TalismanCharge,
    Finisher
}

[DisallowMultipleComponent]
public sealed partial class Ep1TutorialManager : MonoBehaviour
{
    internal static bool IsSkippedForTesting { get; private set; }

    // Keep first-display history when death reloads the EP1 scene.
    private static readonly HashSet<Ep1TutorialHintType> ShownHints = new HashSet<Ep1TutorialHintType>();
    private static readonly HashSet<Ep1TutorialHintType> CompletedHints = new HashSet<Ep1TutorialHintType>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetShownHints()
    {
        IsSkippedForTesting = false;
        ShownHints.Clear();
        CompletedHints.Clear();
        ResetCombatProgress();
    }

    internal static bool HasShownHint(Ep1TutorialHintType hintType) => ShownHints.Contains(hintType);
    internal static bool TryMarkHintShown(Ep1TutorialHintType hintType) => ShownHints.Add(hintType);
    internal static bool HasCompletedHint(Ep1TutorialHintType hintType) => CompletedHints.Contains(hintType);

    internal void CompleteHint(Ep1TutorialHintType hintType)
    {
        CompletedHints.Add(hintType);
        if (hintType == Ep1TutorialHintType.HighJump) _highJumpPracticeActive = false;
    }

#if UNITY_EDITOR
    private void SkipForTesting()
    {
        IsSkippedForTesting = true;
        foreach (Ep1TutorialHintType hintType in System.Enum.GetValues(typeof(Ep1TutorialHintType)))
        {
            ShownHints.Add(hintType);
            CompletedHints.Add(hintType);
        }
        _guardGaugeExplained = true;
        _regularGuardSucceeded = true;
        _soulsExplained = true;
        _secondFinisherSucceeded = true;
        _parryFinisherExplained = true;
        _talismanExplained = true;

        // OnDisable releases practice restrictions, camera focus, pause ownership and hidden enemies.
        enabled = false;
        _combatStep = CombatStep.Complete;
        _checkpointStep = CheckpointStep.Complete;
        _parryFocusStep = ParryFocusStep.Complete;
        _talismanStep = TalismanStep.Complete;
        _startParryHint = false;
    }
#endif

    [SerializeField] private Transform _highJumpHintPoint;
    [SerializeField] private Transform _respawnHintPoint;
    [SerializeField] private Transform _parryHintPoint;
    [SerializeField] private bool _followHintTargets = true;
    [SerializeField] private RespawnPoint _respawnHintTarget;
    [SerializeField, Min(0.1f)] private float _guardEncounterDistance = 8f;

    [SerializeField] private Font _font;
    [SerializeField] private Font _headHintFont;
    [SerializeField, Min(1)] private int _fontSize = 26;
    [SerializeField] private FontStyle _fontStyle = FontStyle.Normal;
    [SerializeField] private Color _textColor = Color.black;
    [SerializeField] private Color _outlineColor = Color.white;
    [SerializeField, Min(0f)] private float _outlineThickness = 1f;

    [SerializeField, TextArea(2, 4)] private string _highJumpMessage = "길게 눌러 더 높이 점프";
    [SerializeField, TextArea(2, 4)] private string _respawnMessage = "장승에 다가가면 리스폰 포인트가 활성화됩니다.\n물약도 모두 충전됩니다.";
    [SerializeField, TextArea(2, 4)] private string _parryMessage = "반짝이는 공격을 패링하면 적을 그로기 상태로 만들 수 있습니다.";
    [SerializeField] private string _keySeparator = "또는";

    [SerializeField] private Vector2 _headOffset = new Vector2(0f, 95f);
    [SerializeField] private Vector2 _jumpTextSize = new Vector2(450f, 52f);
    [SerializeField] private Vector2 _infoTextSize = new Vector2(760f, 100f);
    [SerializeField, Min(0.1f)] private float _triggerDistance = 3f;
    [SerializeField, Min(0.1f)] private float _displayDuration = 6f;
    [SerializeField, Min(0.1f)] private float _holdDuration = 0.3f;

    public int PresentationVersion { get; private set; }
    public Vector2 HeadOffset => _headOffset;
    public Vector2 JumpTextSize => _jumpTextSize;
    public Vector2 InfoTextSize => _infoTextSize;
    public float TriggerDistance => _triggerDistance;
    public float DisplayDuration => _displayDuration;
    public float HoldDuration => _holdDuration;
    public string KeySeparator => _keySeparator;

    private void Awake()
    {
        ResolveRespawnHintTarget();
        AttachHint(_highJumpHintPoint, "Ep1HighJumpHint", Ep1TutorialHintType.HighJump);
        if (_respawnHintPoint != null)
        {
            foreach (Ep1HighJumpHint hint in _respawnHintPoint.GetComponentsInChildren<Ep1HighJumpHint>(true))
                hint.Configure(this, Ep1TutorialHintType.Respawn);
        }

        AttachHint(_parryHintPoint, "Ep1ParryHint", Ep1TutorialHintType.Parry);
        if (_guardHintPoint == null && _parryHintPoint != null)
        {
            _guardHintPoint = new GameObject("GuardHint").transform;
            _guardHintPoint.SetParent(_parryHintPoint.parent, false);
            _guardHintPoint.position = _parryHintPoint.position + Vector3.left * 1.5f;
        }
        AttachHint(_guardHintPoint, "Ep1RespawnHint", Ep1TutorialHintType.Guard);
    }

    public void ResolveHintTargets()
    {
        ResolveRespawnHintTarget();
        ResolveTalismanTutorialTargets();
    }

    private void ResolveRespawnHintTarget()
    {
        if (_respawnHintTarget != null || !gameObject.scene.IsValid()) return;
        RespawnPoint closest = null;
        float closestDistance = float.PositiveInfinity;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            foreach (RespawnPoint point in root.GetComponentsInChildren<RespawnPoint>(true))
            {
                if (point.IsDefault) continue;
                // EP1's first checkpoint keeps its identity when the level layout changes.
                if (point.CheckpointId == "Jangseung_01")
                {
                    _respawnHintTarget = point;
                    return;
                }
                float distance = _respawnHintPoint != null ?
                    ((Vector2)(point.transform.position - _respawnHintPoint.position)).sqrMagnitude :
                    point.transform.position.x;
                if (distance >= closestDistance) continue;
                closest = point;
                closestDistance = distance;
            }
        }
        _respawnHintTarget = closest;
    }

    internal bool IsWithinHintRange(Ep1TutorialHintType hintType, Vector2 playerPosition, Vector2 hintPosition)
    {
        if (_followHintTargets)
        {
            if (hintType == Ep1TutorialHintType.Respawn && _respawnHintTarget != null)
            {
                return _respawnHintTarget.isActiveAndEnabled &&
                    Vector2.Distance(playerPosition, _respawnHintTarget.transform.position) <= _triggerDistance;
            }

            if (hintType == Ep1TutorialHintType.Guard && _boundPrerequisites != null &&
                _boundPrerequisites.Length > 0 && _boundPrerequisites[0] != null)
            {
                EnemyHealth first = _boundPrerequisites[0];
                return first.gameObject.activeInHierarchy && first.IsAlive &&
                    Vector2.Distance(playerPosition, first.transform.position) <= _guardEncounterDistance;
            }
        }
        return Vector2.Distance(playerPosition, hintPosition) <= _triggerDistance;
    }

    private void AttachHint(Transform point, string resourceName, Ep1TutorialHintType hintType)
    {
        if (point == null) return;

        Ep1HighJumpHint hint = point.GetComponentInChildren<Ep1HighJumpHint>(true);
        if (hint == null)
        {
            GameObject prefab = Resources.Load<GameObject>(resourceName);
            if (prefab == null) return;
            GameObject instance = Instantiate(prefab, point, false);
            hint = instance.GetComponent<Ep1HighJumpHint>();
        }

        if (hint != null) hint.Configure(this, hintType);
    }

    public string GetMessage(Ep1TutorialHintType hintType)
    {
        switch (hintType)
        {
            case Ep1TutorialHintType.Respawn: return _respawnMessage;
            case Ep1TutorialHintType.Parry:
                return _requirePracticeInput ?
                    "반짝이는 공격이 닿기 직전에\n아래 방향키를 누르세요. 계속 누르면 가드만 됩니다." : _parryMessage;
            case Ep1TutorialHintType.Guard:
                return _requirePracticeInput ?
                    "아래 방향키를 눌러\n가드를 해보세요." : _guardHoldMessage;
            case Ep1TutorialHintType.TalismanTap: return _talismanTapMessage;
            case Ep1TutorialHintType.TalismanCharge: return _talismanChargeMessage;
            case Ep1TutorialHintType.Finisher: return "노랗게 반짝이는 적에게 다가가\nX를 누르고 있어보세요.";
            default: return _highJumpMessage;
        }
    }

    public void ApplyTextStyle(Text text)
    {
        Font hintFont = _headHintFont != null ? _headHintFont : _font;
        if (hintFont != null) text.font = hintFont;
        text.fontSize = _fontSize;
        text.fontStyle = _fontStyle;
        text.color = _textColor;
        Outline outline = text.GetComponent<Outline>();
        if (outline == null) outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = _outlineColor;
        outline.effectDistance = new Vector2(_outlineThickness, -_outlineThickness);
        outline.enabled = _outlineThickness > 0f;
    }

    private void OnValidate()
    {
        _fontSize = Mathf.Max(1, _fontSize);
        _outlineThickness = Mathf.Max(0f, _outlineThickness);
        _jumpTextSize = new Vector2(Mathf.Max(1f, _jumpTextSize.x), Mathf.Max(1f, _jumpTextSize.y));
        _infoTextSize = new Vector2(Mathf.Max(1f, _infoTextSize.x), Mathf.Max(1f, _infoTextSize.y));
        _triggerDistance = Mathf.Max(0.1f, _triggerDistance);
        _guardEncounterDistance = Mathf.Max(0.1f, _guardEncounterDistance);
        _displayDuration = Mathf.Max(0.1f, _displayDuration);
        _holdDuration = Mathf.Max(0.1f, _holdDuration);
        ValidateCombatSettings();
        ValidateCheckpointSettings();
        ValidateTalismanSettings();
        PresentationVersion++;
    }
}
