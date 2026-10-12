using System;
using UnityEngine;

public sealed partial class Ep1TutorialManager
{
    private enum TalismanStep
    {
        WaitingForEnemies, ZoomingEnemy, EnemyExplanation, ReturningCamera,
        TapPractice, ConfirmingTap, ChargeExplanation, ChargePractice, ConfirmingCharge, Complete
    }

    private static bool _talismanExplained;
    private static readonly System.Collections.Generic.HashSet<EnemyHealth> DeferredTalismanEnemies =
        new System.Collections.Generic.HashSet<EnemyHealth>();
    internal static bool IsEnemySpawnDeferred(EnemyHealth enemy) => DeferredTalismanEnemies.Contains(enemy);
    private static readonly string[] DefaultPrerequisiteNames =
        { "EnemyMeleeGround", "EnemyMeleeGround (1)", "EnemyMeleeGround (2)" };
    private const string DefaultTalismanIntro =
        "V키를 눌러 부적을 날릴 수 있습니다.\n짧게 눌렀다가 떼면 부적 한 장이 발사됩니다.";
    private const string DefaultTalismanTap = "V키를 짧게 눌렀다가 떼면\n부적 한 장이 발사됩니다.";
    private const string DefaultTalismanCharge = "V키를 길게 눌렀다가 떼면\n충전한 부적이 발사됩니다.";

    [SerializeField] private bool _enableTalismanTutorial = true;
    [SerializeField] private EnemyHealth[] _talismanPrerequisiteEnemies = new EnemyHealth[3];
    [SerializeField] private EnemyHealth _talismanTarget;
    [SerializeField] private bool _hideTalismanTargetUntilPrerequisites = true;
    [SerializeField] private Sprite _talismanKeySprite;
    [SerializeField, TextArea(2, 5)] private string _talismanIntroMessage = DefaultTalismanIntro;
    [SerializeField, TextArea(2, 4)] private string _talismanTapMessage = DefaultTalismanTap;
    [SerializeField, TextArea(2, 5)]
    private string _talismanChargeExplanation =
        "V키를 길게 누르면 부적이 충전됩니다.\n키를 떼면 충전한 부적을 한꺼번에 날립니다.\n부적 한 장마다 영혼을 소모합니다.";
    [SerializeField, TextArea(2, 4)] private string _talismanChargeMessage = DefaultTalismanCharge;
    [SerializeField, Min(0f)] private float _talismanVisibleDelay = 0.2f;
    [SerializeField, Min(0f)] private float _talismanMinimumSouls = 3f;
    [SerializeField, Range(0.2f, 0.9f)] private float _talismanZoomFactor = 0.45f;
    [SerializeField, Min(0.01f)] private float _talismanZoomDuration = 0.7f;
    [SerializeField, Min(0.01f)] private float _talismanReturnDuration = 0.5f;
    [SerializeField, Range(0f, 0.2f)] private float _talismanLetterboxAmount = 0.08f;
    [SerializeField, Min(0f)] private float _talismanShotPauseDelay = 0.35f;
    [SerializeField] private bool _refillTalismanPracticeSouls = true;

    private TalismanStep _talismanStep;
    private PlayerRangedAttack _talismanAttack;
    private SpriteRenderer _talismanRenderer;
    private Ep1TutorialCameraFocus _talismanCameraFocus;
    private Ep1HighJumpHint _talismanHeadHint;
    private EnemyHealth[] _boundPrerequisites;
    private Action[] _prerequisiteDeathHandlers;
    private bool[] _prerequisiteDefeated;
    private float _talismanVisibleElapsed;
    private float _talismanShotAt;
    private int _pendingTalismanShotCount;
    private bool _hasCombatEnemySequence;
    private bool _talismanTargetHidden;

    internal Sprite TalismanKeySprite => _talismanKeySprite;
    internal bool IsTalismanTapPractice => _talismanStep == TalismanStep.TapPractice;
    internal bool IsTalismanChargePractice => _talismanStep == TalismanStep.ChargePractice;

    private void InitializeTalismanTutorial()
    {
        UpgradeLegacyTalismanMessages();
        _talismanStep = _talismanExplained ? TalismanStep.Complete : TalismanStep.WaitingForEnemies;
        if (_combatPlayer == null) return;
        _talismanAttack = _combatPlayer.GetComponent<PlayerRangedAttack>();
        ResolveTalismanTutorialTargets();
        _boundPrerequisites = _talismanPrerequisiteEnemies == null ? Array.Empty<EnemyHealth>() :
            (EnemyHealth[])_talismanPrerequisiteEnemies.Clone();
        _prerequisiteDefeated = new bool[_boundPrerequisites.Length];
        _hasCombatEnemySequence = _boundPrerequisites.Length >= 3 && _boundPrerequisites[0] != null &&
            _boundPrerequisites[1] != null && _boundPrerequisites[2] != null;
        for (int i = 0; i < _boundPrerequisites.Length; i++)
        {
            if (_boundPrerequisites[i] != null && _boundPrerequisites[i].gameObject.activeInHierarchy)
                _prerequisiteDefeated[i] = !_boundPrerequisites[i].IsAlive;
        }
        BindTalismanEvents();
        UpdateTalismanTargetAppearance();
    }

    public void ResolveTalismanTutorialTargets()
    {
        if (!gameObject.scene.IsValid()) return;
        if (_talismanPrerequisiteEnemies == null || _talismanPrerequisiteEnemies.Length == 0)
            _talismanPrerequisiteEnemies = new EnemyHealth[3];
        var candidates = new System.Collections.Generic.List<EnemyHealth>();
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            candidates.AddRange(root.GetComponentsInChildren<EnemyHealth>(true));
        if (_talismanTarget == null)
        {
            foreach (EnemyHealth health in candidates)
            {
                if (health.name == "EnemyRangedFlying (3)")
                { _talismanTarget = health; break; }
            }
        }

        for (int i = 0; i < Mathf.Min(DefaultPrerequisiteNames.Length, _talismanPrerequisiteEnemies.Length); i++)
        {
            // An explicitly assigned enemy always wins. Duplicate names can exist far later in EP1.
            if (_talismanPrerequisiteEnemies[i] != null) continue;
            EnemyHealth closest = null;
            float closestDistance = float.PositiveInfinity;
            bool closestIsBeforeTarget = false;
            foreach (EnemyHealth health in candidates)
            {
                if (health.name != DefaultPrerequisiteNames[i]) continue;
                if (_talismanTarget == null) { closest = health; break; }
                Vector2 difference = health.transform.position - _talismanTarget.transform.position;
                bool isBeforeTarget = difference.x <= 0f;
                float distance = difference.sqrMagnitude;
                if (closest != null && (closestIsBeforeTarget && !isBeforeTarget ||
                    closestIsBeforeTarget == isBeforeTarget && distance >= closestDistance))
                {
                    continue;
                }

                closest = health;
                closestDistance = distance;
                closestIsBeforeTarget = isBeforeTarget;
            }
            _talismanPrerequisiteEnemies[i] = closest;
        }
    }

    private void BindTalismanEvents()
    {
        if (_talismanAttack != null)
        {
            _talismanAttack.OnFired -= HandleTalismanFired;
            _talismanAttack.OnFired += HandleTalismanFired;
        }
        if (_boundPrerequisites == null) return;
        // Unity does not restore delegates after recompiling scripts during Play mode.
        if (_prerequisiteDefeated == null || _prerequisiteDefeated.Length != _boundPrerequisites.Length)
            _prerequisiteDefeated = new bool[_boundPrerequisites.Length];
        if (_prerequisiteDeathHandlers == null || _prerequisiteDeathHandlers.Length != _boundPrerequisites.Length)
        {
            _prerequisiteDeathHandlers = new Action[_boundPrerequisites.Length];
            for (int i = 0; i < _boundPrerequisites.Length; i++)
            {
                int index = i;
                _prerequisiteDeathHandlers[i] = () => _prerequisiteDefeated[index] = true;
            }
        }
        for (int i = 0; i < _boundPrerequisites.Length; i++)
        {
            if (_boundPrerequisites[i] == null) continue;
            if (_boundPrerequisites[i].gameObject.activeInHierarchy && !_boundPrerequisites[i].IsAlive)
                _prerequisiteDefeated[i] = true;
            _boundPrerequisites[i].OnDeath -= _prerequisiteDeathHandlers[i];
            _boundPrerequisites[i].OnDeath += _prerequisiteDeathHandlers[i];
        }
    }

    private bool UpdateTalismanTutorial()
    {
        UpdateTalismanTargetAppearance();
        if (!_enableTalismanTutorial || _talismanAttack == null || _talismanStep == TalismanStep.Complete) return false;
        if (_combatPlayer.Health.IsDead || (StageManager.Instance != null && StageManager.Instance.IsRespawning))
        {
            CancelTalismanTutorial();
            return false;
        }
        bool active = _talismanStep != TalismanStep.WaitingForEnemies;
        if (SceneArrivalFade.IsTransitioning) return active;
        if (_talismanStep == TalismanStep.ZoomingEnemy)
        {
            if (_talismanTarget == null || !_talismanTarget.gameObject.activeInHierarchy)
            {
                CancelTalismanTutorial();
                return true;
            }
            if (_talismanCameraFocus.HasReachedTarget)
            {
                _talismanStep = TalismanStep.EnemyExplanation;
                _pageOpenedAt = Time.unscaledTime;
            }
        }
        else if (_talismanStep == TalismanStep.ReturningCamera)
        {
            if (_talismanCameraFocus.HasReturned)
            {
                PauseController.ResumeFromTutorial(this);
                BeginTalismanPractice(TalismanStep.TapPractice);
            }
        }
        else if (_talismanStep == TalismanStep.EnemyExplanation || _talismanStep == TalismanStep.ChargeExplanation)
        {
            if (Time.unscaledTime - _pageOpenedAt >= _enterDelay &&
                (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                AdvanceTalismanPage();
            }
        }
        else if (!PauseController.IsGameplayInputBlocked && _combatInput.enabled && !IsCombatPageOpen)
        {
            if (_talismanStep == TalismanStep.WaitingForEnemies)
            {
                if (!AreTalismanPrerequisitesDefeated() ||
                    _combatPlayer.Soul.CurrentSoul < Mathf.Min(_combatPlayer.Soul.MaxSoul, _talismanMinimumSouls) ||
                    !IsTalismanTargetVisible() ||
                    _talismanAttack.IsCharging || _talismanAttack.IsReadyToFire ||
                    _combatPlayer.StateMachine.CurrentState == _combatPlayer.SpellState)
                {
                    _talismanVisibleElapsed = 0f;
                    return false;
                }
                _talismanVisibleElapsed += Time.deltaTime;
                if (_talismanVisibleElapsed >= _talismanVisibleDelay) BeginTalismanCinematic();
            }
            else if (_talismanStep == TalismanStep.TapPractice || _talismanStep == TalismanStep.ChargePractice)
            {
                if (_pendingTalismanShotCount > 0)
                {
                    bool success = _talismanStep == TalismanStep.TapPractice ? _pendingTalismanShotCount == 1 :
                        _pendingTalismanShotCount >= 2;
                    _pendingTalismanShotCount = 0;
                    if (success)
                    {
                        _talismanStep = _talismanStep == TalismanStep.TapPractice ?
                            TalismanStep.ConfirmingTap : TalismanStep.ConfirmingCharge;
                    }
                }
                else if (!_talismanAttack.IsCharging && !_talismanAttack.IsReadyToFire)
                {
                    EnsureTalismanPracticeSouls(_talismanStep == TalismanStep.TapPractice ? 3 : 2);
                }
            }
            else if (Time.time - _talismanShotAt >= _talismanShotPauseDelay &&
                _combatPlayer.StateMachine.CurrentState != _combatPlayer.SpellState)
            {
                if (_talismanStep == TalismanStep.ConfirmingTap)
                {
                    if (PauseController.TryPauseForTutorial(this))
                    {
                        _talismanStep = TalismanStep.ChargeExplanation;
                        _pageOpenedAt = Time.unscaledTime;
                    }
                }
                else if (_talismanStep == TalismanStep.ConfirmingCharge)
                {
                    _talismanExplained = true;
                    _talismanStep = TalismanStep.Complete;
                }
            }
        }
        return _talismanStep != TalismanStep.WaitingForEnemies && _talismanStep != TalismanStep.Complete;
    }

    private bool AreTalismanPrerequisitesDefeated()
    {
        if (_prerequisiteDefeated == null || _prerequisiteDefeated.Length == 0) return false;
        foreach (bool defeated in _prerequisiteDefeated) if (!defeated) return false;
        return true;
    }

    private bool IsTalismanTargetVisible()
    {
        Camera camera = GetWorldCamera();
        if (_talismanTarget == null || !_talismanTarget.IsAlive || !_talismanTarget.gameObject.activeInHierarchy ||
            camera == null || !camera.orthographic)
        {
            return false;
        }

        if (_talismanRenderer == null)
        {
            Transform visual = _talismanTarget.transform.Find("Visual");
            if (visual != null) _talismanRenderer = visual.GetComponentInChildren<SpriteRenderer>();
            if (_talismanRenderer == null)
            {
                foreach (SpriteRenderer sprite in _talismanTarget.GetComponentsInChildren<SpriteRenderer>())
                {
                    if (sprite.enabled && sprite.sprite != null && sprite.color.a > 0.01f)
                    { _talismanRenderer = sprite; break; }
                }
            }
        }
        if (_talismanRenderer == null || !_talismanRenderer.enabled || _talismanRenderer.sprite == null) return false;
        Vector3 center = camera.WorldToViewportPoint(_talismanRenderer.bounds.center);
        return center.z > 0f && center.x >= 0.08f && center.x <= 0.92f && center.y >= 0.08f && center.y <= 0.92f;
    }

    private void BeginTalismanCinematic()
    {
        if (!PauseController.TryPauseForTutorial(this)) return;
        EnsureTalismanPracticeSouls(3);
        if (_talismanCameraFocus == null) _talismanCameraFocus = gameObject.AddComponent<Ep1TutorialCameraFocus>();
        _talismanCameraFocus.Begin(GetWorldCamera(), _talismanRenderer.bounds,
            _talismanZoomFactor, _talismanZoomDuration, _talismanLetterboxAmount);
        _talismanStep = TalismanStep.ZoomingEnemy;
        if (_focusOverlay == null) _focusOverlay = new Ep1TutorialFocusOverlay(transform);
    }

    private void AdvanceTalismanPage()
    {
        if (_talismanStep == TalismanStep.EnemyExplanation)
        {
            _focusOverlay.Hide();
            _talismanStep = TalismanStep.ReturningCamera;
            _talismanCameraFocus.Return(_talismanReturnDuration);
        }
        else if (_talismanStep == TalismanStep.ChargeExplanation)
        {
            _focusOverlay.Hide();
            PauseController.ResumeFromTutorial(this);
            BeginTalismanPractice(TalismanStep.ChargePractice);
        }
    }

    private void BeginTalismanPractice(TalismanStep step)
    {
        _talismanStep = step;
        _pendingTalismanShotCount = 0;
        EnsureTalismanPracticeSouls(step == TalismanStep.TapPractice ? 3 : 2);
        if (_talismanHeadHint == null)
        {
            GameObject prefab = Resources.Load<GameObject>("Ep1RespawnHint");
            if (prefab == null) return;
            _talismanHeadHint = Instantiate(prefab, transform, false).GetComponent<Ep1HighJumpHint>();
        }
        _talismanHeadHint.Configure(this, step == TalismanStep.TapPractice ?
            Ep1TutorialHintType.TalismanTap : Ep1TutorialHintType.TalismanCharge);
    }

    private void EnsureTalismanPracticeSouls(int count)
    {
        if (!_refillTalismanPracticeSouls || _combatPlayer == null || _talismanAttack == null) return;
        float missing = Mathf.Min(_combatPlayer.Soul.MaxSoul, count * _talismanAttack.SoulCostPerTalisman) -
            _combatPlayer.Soul.CurrentSoul;
        if (missing > 0f) _combatPlayer.Soul.AddSoul(missing);
    }

    private void HandleTalismanFired(int count)
    {
        if (!IsTalismanTapPractice && !IsTalismanChargePractice) return;
        _pendingTalismanShotCount = count;
        _talismanShotAt = Time.time;
        // Let the firing animation/projectile creation finish before any tutorial pause.
    }

    private void RefreshTalismanPage()
    {
        bool enemyPage = _talismanStep == TalismanStep.EnemyExplanation;
        if (!enemyPage && _talismanStep != TalismanStep.ChargeExplanation) return;
        if (_focusOverlay == null) _focusOverlay = new Ep1TutorialFocusOverlay(transform);
        _focusOverlay.Show(enemyPage ? null : _soulGaugeFocus, null, GetWorldCamera(),
            enemyPage ? _talismanIntroMessage : _talismanChargeExplanation, _continueMessage, _continueKeySprite,
            _font, _popupFontSize, _popupFontStyle, _popupTextColor, _popupOutlineColor,
            _popupOutlineThickness, enemyPage ? Mathf.Min(_dimOpacity, 0.4f) : _dimOpacity,
            _focusBorderColor, _focusPadding, _popupTextSize, 24f, new Vector2(240f, 180f),
            enemyPage ? _talismanRenderer : null);
    }

    private void CancelTalismanTutorial()
    {
        bool wasActive = _talismanStep != TalismanStep.WaitingForEnemies && _talismanStep != TalismanStep.Complete;
        if (_talismanCameraFocus != null) _talismanCameraFocus.Cancel();
        if (wasActive)
        {
            if (_focusOverlay != null) _focusOverlay.Hide();
            PauseController.ResumeFromTutorial(this);
        }
        _talismanStep = _talismanExplained ? TalismanStep.Complete : TalismanStep.WaitingForEnemies;
        _pendingTalismanShotCount = 0;
        _talismanVisibleElapsed = 0f;
        if (_talismanHeadHint != null) Destroy(_talismanHeadHint.gameObject);
        _talismanHeadHint = null;
    }

    private void UnbindTalismanEvents()
    {
        RestoreHiddenTalismanTarget();
        if (_talismanAttack != null) _talismanAttack.OnFired -= HandleTalismanFired;
        if (_boundPrerequisites != null && _prerequisiteDeathHandlers != null)
        {
            for (int i = 0; i < _boundPrerequisites.Length; i++)
            {
                if (i < _prerequisiteDeathHandlers.Length && _boundPrerequisites[i] != null)
                    _boundPrerequisites[i].OnDeath -= _prerequisiteDeathHandlers[i];
            }
        }

        CancelTalismanTutorial();
    }

    private void UpdateTalismanTargetAppearance()
    {
        if (!_enableTalismanTutorial || !_hideTalismanTargetUntilPrerequisites || !_hasCombatEnemySequence)
        {
            RestoreHiddenTalismanTarget();
            return;
        }
        if (_talismanTarget == null) return;
        if (AreTalismanPrerequisitesDefeated())
        {
            RestoreHiddenTalismanTarget();
        }
        else
        {
            DeferredTalismanEnemies.Add(_talismanTarget);
            if (_talismanTarget.gameObject.activeSelf)
            {
                _talismanTargetHidden = true;
                _talismanTarget.gameObject.SetActive(false);
            }
        }
    }

    private void RestoreHiddenTalismanTarget()
    {
        DeferredTalismanEnemies.Remove(_talismanTarget);
        if (!_talismanTargetHidden) return;
        _talismanTargetHidden = false;
        if (_talismanTarget != null) _talismanTarget.gameObject.SetActive(true);
    }

    private void ValidateTalismanSettings()
    {
        UpgradeLegacyTalismanMessages();
        _talismanVisibleDelay = Mathf.Max(0f, _talismanVisibleDelay);
        _talismanMinimumSouls = Mathf.Max(0f, _talismanMinimumSouls);
        _talismanZoomFactor = Mathf.Clamp(_talismanZoomFactor, 0.2f, 0.9f);
        _talismanZoomDuration = Mathf.Max(0.01f, _talismanZoomDuration);
        _talismanReturnDuration = Mathf.Max(0.01f, _talismanReturnDuration);
        _talismanLetterboxAmount = Mathf.Clamp(_talismanLetterboxAmount, 0f, 0.2f);
        _talismanShotPauseDelay = Mathf.Max(0f, _talismanShotPauseDelay);
    }

    private void UpgradeLegacyTalismanMessages()
    {
        // Replace only our previous defaults; custom Inspector wording stays editable.
        if (_talismanIntroMessage == "공중의 요괴에게는 부적 공격을 사용해보세요.\nV키를 짧게 눌렀다가 떼면 부적 한 장을 날릴 수 있습니다.")
            _talismanIntroMessage = DefaultTalismanIntro;
        if (_talismanTapMessage == "V키를 짧게 눌렀다가 떼어\n부적 한 장을 날려보세요.")
            _talismanTapMessage = DefaultTalismanTap;
        if (_talismanChargeMessage == "V키를 길게 눌렀다가 떼어\n부적 두 장 이상을 날려보세요.")
            _talismanChargeMessage = DefaultTalismanCharge;
    }
}
