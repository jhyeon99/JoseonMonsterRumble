using UnityEngine;
using UnityEngine.UI;

public enum Ep1TutorialHintType
{
    HighJump,
    Respawn,
    Parry
}

[DisallowMultipleComponent]
public sealed class Ep1TutorialManager : MonoBehaviour
{
    [SerializeField] private Transform _highJumpHintPoint;
    [SerializeField] private Transform _respawnHintPoint;
    [SerializeField] private Transform _parryHintPoint;

    [SerializeField] private Font _font;
    [SerializeField, Min(1)] private int _fontSize = 26;
    [SerializeField] private FontStyle _fontStyle = FontStyle.Normal;
    [SerializeField] private Color _textColor = Color.black;
    [SerializeField] private Color _outlineColor = Color.white;
    [SerializeField, Min(0f)] private float _outlineThickness = 1f;

    [SerializeField, TextArea(2, 4)] private string _highJumpMessage = "길게 눌러 더 높이 점프";
    [SerializeField, TextArea(2, 4)] private string _respawnMessage = "장승에 다가가면 리스폰 포인트가 활성화됩니다.";
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
        AttachHint(_highJumpHintPoint, "Ep1HighJumpHint", Ep1TutorialHintType.HighJump);
        AttachHint(_respawnHintPoint, "Ep1RespawnHint", Ep1TutorialHintType.Respawn);
        AttachHint(_parryHintPoint, "Ep1ParryHint", Ep1TutorialHintType.Parry);
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
            case Ep1TutorialHintType.Parry: return _parryMessage;
            default: return _highJumpMessage;
        }
    }

    public void ApplyTextStyle(Text text)
    {
        if (_font != null) text.font = _font;
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
        _displayDuration = Mathf.Max(0.1f, _displayDuration);
        _holdDuration = Mathf.Max(0.1f, _holdDuration);
        PresentationVersion++;
    }
}
