using System.Collections.Generic;
using UnityEngine;

public sealed partial class Ep1TutorialManager
{
    private enum CombatStep
    {
        WaitingForHint,
        GuardGauge,
        WaitingForRegularGuard,
        WaitingForParryHint,
        WaitingForFinisher,
        WaitingForDeathSouls,
        DeathSoulsFocus,
        WaitingForCollection,
        SoulGauge,
        Complete,
        WaitingForFirstEnemy,
        GuardExplanation
    }

    private static bool _guardGaugeExplained;
    private static bool _regularGuardSucceeded;
    private static bool _soulsExplained;
    private static bool _secondFinisherSucceeded;

    [SerializeField] private Transform _guardHintPoint;
    [SerializeField] private RectTransform _guardGaugeFocus;
    [SerializeField] private RectTransform _soulGaugeFocus;
    [SerializeField] private Sprite _guardKeySprite;
    [SerializeField] private Sprite _continueKeySprite;

    [SerializeField, TextArea(2, 4)] private string _guardHoldMessage = "아래 방향키를 길게 눌러 가드하세요.";
    [SerializeField, TextArea(2, 6)] private string _guardGaugeMessage = "가드를 사용하면 게이지가 닳습니다.\n게이지를 다 사용하게 되면, 가드를 해도 데미지를 입습니다.";
    [SerializeField, TextArea(2, 4)] private string _soulDropMessage = "요괴 처치 시, 영혼을 습득합니다.";
    [SerializeField, TextArea(2, 4)] private string _soulGaugeMessage = "습득한 영혼은 이곳에서 확인 할 수 있습니다.";
    [SerializeField] private string _continueMessage = "Enter를 눌러 계속";
    [SerializeField, Min(0.1f)] private float _parryEncounterDistance = 8f;

    [SerializeField, Min(0f)] private float _enterDelay = 0.2f;
    [SerializeField, Range(0f, 1f)] private float _dimOpacity = 0.65f;
    [SerializeField, Min(1)] private int _popupFontSize = 30;
    [SerializeField] private FontStyle _popupFontStyle = FontStyle.Normal;
    [SerializeField] private Color _popupTextColor = new Color(0.96f, 0.94f, 0.87f, 1f);
    [SerializeField] private Color _popupOutlineColor = Color.black;
    [SerializeField, Min(0f)] private float _popupOutlineThickness = 1f;
    [SerializeField] private Color _focusBorderColor = new Color(0.75f, 0.85f, 1f, 1f);
    [SerializeField, Min(0f)] private float _focusPadding = 8f;
    [SerializeField] private Vector2 _popupTextSize = new Vector2(650f, 170f);
    [SerializeField, Range(0f, 0.4f)] private float _soulFocusDelay = 0.25f;
    [SerializeField, Min(0f)] private float _soulFocusPadding = 32f;
    [SerializeField] private Vector2 _soulFocusMinimumSize = new Vector2(240f, 180f);

    private readonly HashSet<SoulItem> _tutorialSouls = new HashSet<SoulItem>();
    private CombatStep _combatStep;
    private Player _combatPlayer;
    private PlayerGuard _guard;
    private PlayerInputHandler _combatInput;
    private Ep1TutorialFocusOverlay _focusOverlay;
    private SoulItem[] _pendingDeathSouls;
    private SoulItem[] _finisherFrameSouls;
    private int _finisherSoulFrame = -1;
    private Camera _worldCamera;
    private float _soulFocusElapsed;
    private float _pageOpenedAt;
    private bool _startParryHint;

    public Sprite GuardKeySprite => _guardKeySprite;
    internal bool ShouldStartParryHint => _startParryHint && CanShowHint(Ep1TutorialHintType.Parry);

    private static void ResetCombatProgress()
    {
        _guardGaugeExplained = false;
        _regularGuardSucceeded = false;
        _soulsExplained = false;
        _secondFinisherSucceeded = false;
        _parryFinisherExplained = false;
        _talismanExplained = false;
        DeferredTalismanEnemies.Clear();
    }

    private void Start()
    {
        _combatPlayer = FindFirstObjectByType<Player>();
        if (_combatPlayer == null) return;
        _guard = _combatPlayer.GetComponent<PlayerGuard>();
        _combatInput = _combatPlayer.GetComponent<PlayerInputHandler>();
        if (_guard == null || _combatInput == null) return;

        ResolveFocusReferences();
        InitializeTalismanTutorial();
        InitializeCheckpointTutorial();
        _combatStep = _guardGaugeExplained ? GetPostGuardStep() :
            _regularGuardSucceeded ? CombatStep.WaitingForRegularGuard : CombatStep.WaitingForHint;
        _startParryHint = _combatStep == CombatStep.WaitingForParryHint;
        BindCombatEvents();
#if UNITY_EDITOR
        if (IsSkippedForTesting || MapTestMode.IsEnabled)
        {
            SkipForTesting();
            return;
        }
#endif
        UpdateRequiredPractice();
    }

    private void OnEnable()
    {
        if (_guard != null) BindCombatEvents();
        if (_talismanAttack != null || _boundPrerequisites != null) BindTalismanEvents();
    }

    private void BindCombatEvents()
    {
        _guard.OnBlocked -= HandleRegularGuard;
        _guard.OnBlocked += HandleRegularGuard;
        _guard.OnParried -= HandleTutorialParry;
        _guard.OnParried += HandleTutorialParry;
        ItemPool.OnDeathSoulsSpawned -= HandleDeathSouls;
        ItemPool.OnDeathSoulsSpawned += HandleDeathSouls;
        ItemPool.OnEnemyDeathSoulsSpawned -= HandleEnemyDeathSouls;
        ItemPool.OnEnemyDeathSoulsSpawned += HandleEnemyDeathSouls;
        SoulItem.OnCollected -= HandleSoulCollected;
        SoulItem.OnCollected += HandleSoulCollected;
        GameEvents.OnFinisherImpact -= HandleTutorialFinisher;
        GameEvents.OnFinisherImpact += HandleTutorialFinisher;
    }

    internal bool CanShowHint(Ep1TutorialHintType type)
    {
        if (IsSkippedForTesting) return false;
        if (type == Ep1TutorialHintType.Finisher) return IsFinisherPractice && !IsParryTutorialPending;
        if (type == Ep1TutorialHintType.Respawn) return false;
        if (IsParryTutorialPending || IsCheckpointCinematicActive || IsCheckpointIntroductionPending ||
            (type != Ep1TutorialHintType.HighJump && IsCheckpointTutorialPending))
        {
            return false;
        }

        if (type == Ep1TutorialHintType.TalismanTap) return IsTalismanTapPractice;
        if (type == Ep1TutorialHintType.TalismanCharge) return IsTalismanChargePractice;
        if (type == Ep1TutorialHintType.Guard)
            return !_guardGaugeExplained && !IsHighJumpPracticePending && !IsGuardDemonstrationActive;
        if (type == Ep1TutorialHintType.Parry)
        {
            if (_parryFinisherExplained) return false;
            if (_hasCombatEnemySequence)
            {
                return _guardGaugeExplained && _regularGuardSucceeded && _soulsExplained &&
                    !_secondFinisherSucceeded && _prerequisiteDefeated[0] &&
                    !_prerequisiteDefeated[1] && IsSecondEnemyEncountered();
            }

            return (_guardGaugeExplained && _regularGuardSucceeded) || _guardHintPoint == null || _guard == null;
        }
        return true;
    }

    internal void NotifyHintDisplayed(Ep1TutorialHintType type)
    {
        if (type == Ep1TutorialHintType.HighJump) _highJumpPracticeActive = true;
        if (type == Ep1TutorialHintType.Guard && !_guardGaugeExplained)
            _combatStep = CombatStep.WaitingForRegularGuard;
        if (type == Ep1TutorialHintType.Parry)
        {
            _startParryHint = false;
            if (_combatStep == CombatStep.WaitingForParryHint)
                _combatStep = CombatStep.WaitingForFinisher;
        }
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (IsSkippedForTesting || MapTestMode.IsEnabled)
        {
            SkipForTesting();
            return;
        }
#endif
        if (_guard == null || _combatPlayer == null || _combatInput == null) return;
        if (_combatPlayer.Health.IsDead || (StageManager.Instance != null && StageManager.Instance.IsRespawning))
        {
            CancelCheckpointTutorial();
            CancelParryTutorial();
            CancelTalismanTutorial();
            CancelCombatPage();
            ReleaseRequiredPractice();
            return;
        }
        UpdateRequiredPractice(true);
        if (UpdateCheckpointTutorial() || IsCheckpointTutorialPending) return;
        if (UpdateParryTutorial()) return;
        if (UpdateTalismanTutorial()) return;
        if (_combatStep == CombatStep.WaitingForFirstEnemy && _prerequisiteDefeated[0])
        {
            _combatStep = GetPostGuardStep();
            _startParryHint = _combatStep == CombatStep.WaitingForParryHint;
        }
        if (IsCombatPageOpen)
        {
            if (Time.unscaledTime - _pageOpenedAt >= _enterDelay &&
                (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                AdvanceCombatPage();
            }

            return;
        }
        if (_combatStep == CombatStep.WaitingForRegularGuard && _regularGuardSucceeded)
        {
            // Explain the demonstrated block first, then focus the gauge on the next page.
            OpenCombatPage(CombatStep.GuardExplanation);
            return;
        }
        if (PauseController.IsGameplayInputBlocked || SceneArrivalFade.IsTransitioning || !_combatInput.enabled)
            return;

        if (_combatStep == CombatStep.WaitingForDeathSouls && _pendingDeathSouls != null)
        {
            _soulFocusElapsed += Time.deltaTime;
            if (_tutorialSouls.Count == 0)
            {
                _pendingDeathSouls = null;
                _combatStep = CombatStep.WaitingForCollection;
            }
            else if (_soulFocusElapsed >= _soulFocusDelay &&
                Ep1TutorialFocusOverlay.AreSoulsVisible(_pendingDeathSouls, GetWorldCamera()))
            {
                OpenCombatPage(CombatStep.DeathSoulsFocus);
            }
        }
        else if (_combatStep == CombatStep.WaitingForCollection && _tutorialSouls.Count == 0)
        {
            // A collection can occur during a scene arrival or pause transition; defer its card.
            OpenCombatPage(CombatStep.SoulGauge);
        }
    }

    private bool IsCombatPageOpen => _combatStep == CombatStep.GuardExplanation || _combatStep == CombatStep.GuardGauge ||
        _combatStep == CombatStep.DeathSoulsFocus || _combatStep == CombatStep.SoulGauge;

    private void LateUpdate()
    {
        if (_combatPlayer != null && !_combatPlayer.Health.IsDead &&
            (StageManager.Instance == null || !StageManager.Instance.IsRespawning))
        {
            UpdateRequiredPractice();
        }
        if (IsParryCinematicActive)
        {
            RefreshParryPage();
            return;
        }
        if (IsCheckpointCinematicActive)
        {
            RefreshCheckpointPage();
            return;
        }
        RefreshTalismanPage();
        if (IsGuardDemonstrationActive && !IsCombatPageOpen)
        {
            RefreshGuardDemonstration();
            return;
        }
        if (!IsCombatPageOpen || _focusOverlay == null) return;
        RefreshCombatPage();
    }

    private void HandleRegularGuard(HitData hit)
    {
        if (_combatStep != CombatStep.WaitingForRegularGuard || _soulsExplained) return;
        if (_requirePracticeInput)
        {
            if (!_guardPracticeAttackStarted || !IsGuardDemonstrationActive || !_guard.IsGuarding) return;
            var source = hit.Source as Component;
            if (_guardPracticeEnemy != null &&
                (source == null || source.GetComponentInParent<Enemy>() != _guardPracticeEnemy))
            {
                return;
            }
            CompleteHint(Ep1TutorialHintType.Guard);
        }
        _regularGuardSucceeded = true;
        // Defer pausing to Update so the successful block's combat events can finish.
        if (_guardGaugeExplained)
        {
            _combatStep = GetPostGuardStep();
            _startParryHint = _combatStep == CombatStep.WaitingForParryHint;
        }
    }

    private CombatStep GetPostGuardStep()
    {
        if (!_regularGuardSucceeded) return CombatStep.WaitingForRegularGuard;
        if (_hasCombatEnemySequence)
        {
            if (!_prerequisiteDefeated[0]) return CombatStep.WaitingForFirstEnemy;
            if (!_soulsExplained) return CombatStep.WaitingForDeathSouls;
            if (_secondFinisherSucceeded || (!_requirePracticeInput && _prerequisiteDefeated[1])) return CombatStep.Complete;
        }
        else if (_soulsExplained)
        {
            return CombatStep.Complete;
        }

        return HasShownHint(Ep1TutorialHintType.Parry) || _parryHintPoint == null
            ? CombatStep.WaitingForFinisher : CombatStep.WaitingForParryHint;
    }

    private void HandleTutorialFinisher(Transform attacker, Transform target)
    {
        if (_combatStep != CombatStep.WaitingForFinisher || _combatPlayer == null ||
            attacker != _combatPlayer.transform || target == null)
        {
            return;
        }

        if (_hasCombatEnemySequence)
        {
            if (_boundPrerequisites[1] == null || target != _boundPrerequisites[1].transform) return;
            if (_requirePracticeInput && (!IsFinisherPractice || _boundPrerequisites[1].IsAlive)) return;
            _secondFinisherSucceeded = true;
            CompleteHint(Ep1TutorialHintType.Finisher);
            _heldPracticeGroggy = null;
            _combatStep = CombatStep.Complete;
            _startParryHint = false;
            return;
        }
        _combatStep = CombatStep.WaitingForDeathSouls;
        // ReceiveFinisher spawns normal enemy drops before OnFinisherImpact fires.
        // Only accept that frame's drops; earlier ordinary kills must not advance the tutorial.
        SoulItem[] souls = _finisherSoulFrame == Time.frameCount ? _finisherFrameSouls : null;
        _finisherFrameSouls = null;
        _finisherSoulFrame = -1;
        if (souls != null) HandleDeathSouls(souls);
    }

    private void HandleDeathSouls(SoulItem[] souls)
    {
        // Sequenced EP1 uses source-aware drops from the first enemy, for any killing attack.
        if (_hasCombatEnemySequence) return;
        if (!_regularGuardSucceeded || souls == null || souls.Length == 0 || _pendingDeathSouls != null) return;
        if (_combatStep == CombatStep.WaitingForFinisher)
        {
            _finisherFrameSouls = souls;
            _finisherSoulFrame = Time.frameCount;
            return;
        }
        if (_combatStep != CombatStep.WaitingForDeathSouls) return;
        CaptureDeathSouls(souls);
    }

    private void HandleEnemyDeathSouls(EnemyHealth source, SoulItem[] souls)
    {
        if (!_hasCombatEnemySequence || source != _boundPrerequisites[0] || _soulsExplained ||
            !_regularGuardSucceeded || souls == null || souls.Length == 0 || _pendingDeathSouls != null)
        {
            return;
        }

        if (_combatStep != CombatStep.WaitingForFirstEnemy && _combatStep != CombatStep.GuardGauge &&
            _combatStep != CombatStep.WaitingForRegularGuard && _combatStep != CombatStep.WaitingForDeathSouls)
        {
            return;
        }

        CaptureDeathSouls(souls);
        // A kill can finish during the block callback, before the gauge card opens in Update.
        if (_guardGaugeExplained) _combatStep = CombatStep.WaitingForDeathSouls;
    }

    private void CaptureDeathSouls(SoulItem[] souls)
    {
        _pendingDeathSouls = souls;
        _soulFocusElapsed = 0f;
        _tutorialSouls.Clear();
        foreach (SoulItem soul in souls)
        {
            if (soul != null) _tutorialSouls.Add(soul);
        }
        // Death drops initially share one position. Let the real drops scatter briefly
        // before freezing; the focus opening follows their world renderers, not UI copies.
    }

    private void HandleSoulCollected(SoulItem soul)
    {
        if (!_tutorialSouls.Contains(soul)) return;
        if (_combatStep == CombatStep.WaitingForDeathSouls)
        {
            // Off-screen kills can be collected before any drop becomes visible.
            _tutorialSouls.Remove(soul);
            return;
        }
        if (_combatStep != CombatStep.WaitingForCollection) return;
        // The drop has actually entered the player's soul gauge, so its real value is visible.
        _tutorialSouls.Clear();
        if (!SceneArrivalFade.IsTransitioning && !PauseController.IsPaused)
            OpenCombatPage(CombatStep.SoulGauge);
    }

    private void OpenCombatPage(CombatStep page)
    {
        ResolveFocusReferences();
        RectTransform focus = page == CombatStep.GuardGauge ? _guardGaugeFocus : _soulGaugeFocus;
        if (page != CombatStep.DeathSoulsFocus && page != CombatStep.GuardExplanation && focus == null)
        {
            Debug.LogWarning($"{name}: 튜토리얼 강조 UI 연결이 없어 안내를 화면 중앙에 표시합니다.", this);
        }
        if (!PauseController.TryPauseForTutorial(this)) return;
        _combatStep = page;
        _pageOpenedAt = Time.unscaledTime;
        if (_focusOverlay == null) _focusOverlay = new Ep1TutorialFocusOverlay(transform);
        RefreshCombatPage();
    }

    private void RefreshCombatPage()
    {
        if (_combatStep == CombatStep.GuardExplanation)
        {
            RefreshGuardDemonstration(true);
            return;
        }
        string message = _combatStep == CombatStep.GuardGauge ? _guardGaugeMessage :
            _combatStep == CombatStep.DeathSoulsFocus ? _soulDropMessage : _soulGaugeMessage;
        RectTransform focus = _combatStep == CombatStep.GuardGauge ? _guardGaugeFocus : _soulGaugeFocus;
        bool worldFocus = _combatStep == CombatStep.DeathSoulsFocus;
        _focusOverlay.Show(worldFocus ? null : focus,
            worldFocus ? _pendingDeathSouls : null, GetWorldCamera(), message, _continueMessage, _continueKeySprite,
            _font, _popupFontSize, _popupFontStyle, _popupTextColor, _popupOutlineColor,
            _popupOutlineThickness, _dimOpacity, _focusBorderColor, _focusPadding,
            _popupTextSize, _soulFocusPadding, _soulFocusMinimumSize);
    }

    private void AdvanceCombatPage()
    {
        CombatStep previous = _combatStep;
        if (previous == CombatStep.GuardExplanation)
        {
            _combatStep = CombatStep.GuardGauge;
            _pageOpenedAt = Time.unscaledTime;
            RefreshCombatPage();
            return;
        }
        _focusOverlay.Hide();
        PauseController.ResumeFromTutorial(this);
        if (previous == CombatStep.GuardGauge)
        {
            _guardGaugeExplained = true;
            ReleaseGuardDemonstration();
            TryMarkHintShown(Ep1TutorialHintType.Guard);
            _combatStep = GetPostGuardStep();
            _startParryHint = _combatStep == CombatStep.WaitingForParryHint;
        }
        else if (previous == CombatStep.DeathSoulsFocus)
        {
            _combatStep = CombatStep.WaitingForCollection;
            _pendingDeathSouls = null;
        }
        else
        {
            _soulsExplained = true;
            _combatStep = GetPostGuardStep();
            _startParryHint = _combatStep == CombatStep.WaitingForParryHint;
            _tutorialSouls.Clear();
        }
    }

    private void CancelCombatPage()
    {
        bool guardHintWasActive = _combatStep == CombatStep.WaitingForRegularGuard ||
            _combatStep == CombatStep.GuardExplanation || _combatStep == CombatStep.GuardGauge;
        ReleaseGuardDemonstration();
        if (_focusOverlay != null) _focusOverlay.Hide();
        PauseController.ResumeFromTutorial(this);
        _pendingDeathSouls = null;
        _finisherFrameSouls = null;
        _finisherSoulFrame = -1;
        _soulFocusElapsed = 0f;
        _tutorialSouls.Clear();
        _combatStep = _guardGaugeExplained ? GetPostGuardStep() :
            guardHintWasActive ? CombatStep.WaitingForRegularGuard : CombatStep.WaitingForHint;
        _startParryHint = _combatStep == CombatStep.WaitingForParryHint;
    }

    private void OnDisable()
    {
        ReleaseRequiredPractice();
        CancelCheckpointTutorial();
        CancelParryTutorial();
        UnbindTalismanEvents();
        if (_guard != null) _guard.OnBlocked -= HandleRegularGuard;
        if (_guard != null) _guard.OnParried -= HandleTutorialParry;
        ItemPool.OnDeathSoulsSpawned -= HandleDeathSouls;
        ItemPool.OnEnemyDeathSoulsSpawned -= HandleEnemyDeathSouls;
        SoulItem.OnCollected -= HandleSoulCollected;
        GameEvents.OnFinisherImpact -= HandleTutorialFinisher;
        CancelCombatPage();
    }

    private void OnDestroy()
    {
        if (_focusOverlay != null) _focusOverlay.Dispose();
    }

    private void ResolveFocusReferences()
    {
        if (_guardGaugeFocus == null)
        {
            UI_GuardGauge gauge = FindFirstObjectByType<UI_GuardGauge>();
            if (gauge != null) _guardGaugeFocus = gauge.transform as RectTransform;
        }
        if (_soulGaugeFocus == null)
        {
            UI_SoulGauge gauge = FindFirstObjectByType<UI_SoulGauge>();
            if (gauge != null) _soulGaugeFocus = gauge.transform as RectTransform;
        }
    }

    private Camera GetWorldCamera()
    {
        if (_worldCamera == null) _worldCamera = Camera.main;
        return _worldCamera;
    }

    private void ValidateCombatSettings()
    {
        ValidateParrySettings();
        _parryEncounterDistance = Mathf.Max(0.1f, _parryEncounterDistance);
        _enterDelay = Mathf.Max(0f, _enterDelay);
        _popupFontSize = Mathf.Max(1, _popupFontSize);
        _popupOutlineThickness = Mathf.Max(0f, _popupOutlineThickness);
        _focusPadding = Mathf.Max(0f, _focusPadding);
        _popupTextSize = new Vector2(Mathf.Max(100f, _popupTextSize.x), Mathf.Max(50f, _popupTextSize.y));
        _soulFocusDelay = Mathf.Clamp(_soulFocusDelay, 0f, 0.4f);
        _soulFocusPadding = Mathf.Max(0f, _soulFocusPadding);
        _soulFocusMinimumSize = new Vector2(Mathf.Max(1f, _soulFocusMinimumSize.x), Mathf.Max(1f, _soulFocusMinimumSize.y));
    }

    private bool IsSecondEnemyEncountered()
    {
        EnemyHealth second = _boundPrerequisites[1];
        return second != null && second.IsAlive && second.gameObject.activeInHierarchy &&
            _combatPlayer != null && Vector2.Distance(_combatPlayer.transform.position, second.transform.position) <=
            _parryEncounterDistance;
    }

}
