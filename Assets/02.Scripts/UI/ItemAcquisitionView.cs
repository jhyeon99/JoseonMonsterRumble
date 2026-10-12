using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-150)]
public sealed class ItemAcquisitionView : MonoBehaviour
{
    private const string MissingReferencesWarning = "아이템 획득 UI: CanvasGroup, 이미지, 텍스트 또는 확인 버튼 참조가 없습니다.";
    private const string DuplicateViewWarning = "아이템 획득 UI가 중복 배치되었습니다.";
    private enum ViewPhase { Hidden, Opening, AwaitingConfirm, Closing }

    public static ItemAcquisitionView Instance { get; private set; }
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _descriptionText;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private GameObject _gamtuAfterimageIllustration;
    [SerializeField, Min(0f)] private float _openingDuration = 0.2f;
    [SerializeField, Min(0f)] private float _closingDuration = 0.15f;
    [SerializeField, ReadOnly] private ViewPhase _phase;
    private float _elapsedTime;
    private bool _confirmReleased;
    private bool _hasReferences;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance() => Instance = null;

    private void Awake()
    {
        _hasReferences = _group != null && _icon != null && _nameText != null &&
                         _descriptionText != null && _confirmButton != null;
        if (!_hasReferences)
        {
            Debug.LogWarning(MissingReferencesWarning, this);
            enabled = false;
            return;
        }
        _confirmButton.onClick.AddListener(HandleConfirm);
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;
        _phase = ViewPhase.Hidden;
    }

    private void OnEnable()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(DuplicateViewWarning, this);
            enabled = false;
            return;
        }
        if (_hasReferences)
        {
            Instance = this;
        }
    }

    private void OnDisable()
    {
        if (Instance != this)
        {
            return;
        }
        HideImmediate();
        Instance = null;
    }

    private void OnDestroy()
    {
        if (_confirmButton != null)
        {
            _confirmButton.onClick.RemoveListener(HandleConfirm);
        }
    }

    public bool TryAcquireItem(AbilityItemData item)
    {
        if (!_hasReferences || _phase != ViewPhase.Hidden || item == null || !item.IsValid ||
            AbilityItemProgress.HasItem(item.ItemId) || !PauseController.TryStartItemModal())
        {
            return false;
        }
        _icon.sprite = item.Icon;
        _icon.preserveAspect = true;
        _nameText.SetText(item.DisplayName);
        _descriptionText.SetText(item.Description);
        if (_gamtuAfterimageIllustration != null)
        {
            _gamtuAfterimageIllustration.SetActive(item.ItemId == AbilityItemProgress.DokkaebiGamtuId);
        }
        _nameText.ForceMeshUpdate();
        _descriptionText.ForceMeshUpdate();
        _group.blocksRaycasts = true;
        GameCursorController.SetItemAcquisitionVisible(true);
        SetPhase(ViewPhase.Opening);
        if (!AbilityItemProgress.TryAcquireItem(item))
        {
            HideImmediate();
            return false;
        }
        return true;
    }

    private void Update()
    {
        if (_phase == ViewPhase.Hidden)
        {
            return;
        }
        if (StageManager.Instance != null && StageManager.Instance.IsRespawning)
        {
            HideImmediate();
            return;
        }
        _elapsedTime += Time.unscaledDeltaTime;
        switch (_phase)
        {
            case ViewPhase.Opening:
                _group.alpha = _openingDuration > 0f ? Mathf.Clamp01(_elapsedTime / _openingDuration) : 1f;
                if (_group.alpha >= 1f)
                {
                    SetPhase(ViewPhase.AwaitingConfirm);
                }
                break;
            case ViewPhase.AwaitingConfirm:
                if (!_confirmReleased)
                {
                    _confirmReleased = !IsConfirmHeld();
                    _group.interactable = _confirmReleased;
                }
                else if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
                         Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    HandleConfirm();
                }
                break;
            case ViewPhase.Closing:
                _group.alpha = _closingDuration > 0f ? 1f - Mathf.Clamp01(_elapsedTime / _closingDuration) : 0f;
                if (_group.alpha <= 0f)
                {
                    HideImmediate();
                }
                break;
        }
    }

    private static bool IsConfirmHeld() => Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return) ||
                                         Input.GetKey(KeyCode.KeypadEnter) || Input.GetMouseButton(0);

    private void HandleConfirm()
    {
        if (_phase == ViewPhase.AwaitingConfirm && _confirmReleased)
        {
            SetPhase(ViewPhase.Closing);
        }
    }

    private void SetPhase(ViewPhase phase)
    {
        _phase = phase;
        _elapsedTime = 0f;
        _confirmReleased = false;
        _group.interactable = false;
    }

    private void HideImmediate()
    {
        bool wasVisible = _phase != ViewPhase.Hidden;
        SetPhase(ViewPhase.Hidden);
        if (_group != null)
        {
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
        }
        if (wasVisible)
        {
            PauseController.StopItemModal();
            GameCursorController.SetItemAcquisitionVisible(false);
        }
    }
}
