using UnityEngine;

/// <summary>
/// 첫 세이브(튜토리얼 장승 활성화) 직후 물약 사용법 안내.
/// 물약 UI를 강조한 설명 카드 → 체력을 깎아 둔 상태에서 1키로 물약을 마셔보는 연습 → 완료 시 물약을 다시 채움
/// </summary>
public sealed partial class Ep1TutorialManager
{
    private enum PotionStep { WaitingForSave, Explanation, Practice, Confirming, Complete }

    [SerializeField] private bool _enablePotionTutorial = true;
    [SerializeField] private RectTransform _potionFocus;
    [SerializeField] private Sprite _potionKeySprite;
    [SerializeField, TextArea(2, 5)]
    private string _potionExplanation =
        "1키를 눌러 물약을 마시면 체력을 회복합니다.\n남은 물약은 이곳에서 확인할 수 있고,\n장승에서 다시 채워집니다.";
    [SerializeField, TextArea(2, 4)] private string _potionPracticeMessage = "1키를 눌러\n물약을 마셔보세요.";
    [SerializeField, Min(0f)] private float _potionTutorialDelay = 0.6f;
    [SerializeField, Range(0.05f, 0.95f)] private float _potionPracticeHealthRatio = 0.5f;

    private PotionStep _potionStep;
    private Ep1HighJumpHint _potionHeadHint;
    private PlayerHealth _potionHealth;
    private float _potionWaitElapsed;
    private bool _potionUsedInPractice;

    internal Sprite PotionKeySprite => _potionKeySprite;
    internal bool IsPotionPractice => _potionStep == PotionStep.Practice;
    private bool IsPotionTutorialActive => _potionStep == PotionStep.Explanation ||
        _potionStep == PotionStep.Practice || _potionStep == PotionStep.Confirming;

    private void InitializePotionTutorial()
    {
        _potionHealth = _combatPlayer != null ? _combatPlayer.Health : null;
        _potionStep = !_enablePotionTutorial || _potionHealth == null || _respawnHintTarget == null ||
            HasCompletedHint(Ep1TutorialHintType.PotionUse) ? PotionStep.Complete : PotionStep.WaitingForSave;
        _potionWaitElapsed = 0f;
        _potionUsedInPractice = false;
        BindPotionEvents();
    }

    private void BindPotionEvents()
    {
        if (_potionHealth == null) return;
        _potionHealth.OnPotionUsed -= HandleTutorialPotionUsed;
        _potionHealth.OnPotionUsed += HandleTutorialPotionUsed;
    }

    private void UnbindPotionEvents()
    {
        if (_potionHealth != null)
        {
            _potionHealth.OnPotionUsed -= HandleTutorialPotionUsed;
        }
        CancelPotionTutorial();
    }

    /// <summary>진행 중이면 true (다른 튜토리얼 시작을 막음)</summary>
    private bool UpdatePotionTutorial()
    {
        if (_potionStep == PotionStep.Complete || _potionHealth == null) return false;

        if (_potionStep == PotionStep.Explanation)
        {
            if (Time.unscaledTime - _pageOpenedAt >= _enterDelay &&
                (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                AdvancePotionPage();
            }
            return true;
        }

        if (PauseController.IsGameplayInputBlocked || SceneArrivalFade.IsTransitioning || !_combatInput.enabled)
            return IsPotionTutorialActive;

        if (_potionStep == PotionStep.WaitingForSave)
        {
            // 실제로 장승이 활성화(세이브)된 뒤, 다른 연습이 진행 중이 아닐 때 서 있는 상태에서 시작
            if (!IsTutorialCheckpointActivated || IsOtherPracticeActive() || !IsPlayerReadyForPotionPage())
            {
                _potionWaitElapsed = 0f;
                return false;
            }
            _potionWaitElapsed += Time.deltaTime;
            if (_potionWaitElapsed >= _potionTutorialDelay)
            {
                OpenPotionPage();
            }
            return _potionStep != PotionStep.WaitingForSave;
        }

        if (_potionStep == PotionStep.Practice)
        {
            if (_potionUsedInPractice)
            {
                _potionStep = PotionStep.Confirming;
            }
            else
            {
                EnsurePotionPracticeState();
            }
            return true;
        }

        // Confirming: 마시는 동작이 끝난 뒤 완료 처리
        if (_combatPlayer.StateMachine.CurrentState == _combatPlayer.PotionState) return true;
        CompletePotionTutorial();
        return false;
    }

    private bool IsOtherPracticeActive()
    {
        return (_highJumpPracticeActive && IsHighJumpPracticePending) || IsGuardDemonstrationActive ||
            IsCombatPageOpen || IsParryCinematicActive ||
            (_talismanStep != TalismanStep.WaitingForEnemies && _talismanStep != TalismanStep.Complete);
    }

    private bool IsPlayerReadyForPotionPage()
    {
        if (_combatPlayer.Health.IsDead || !_combatPlayer.IsGrounded) return false;
        IState state = _combatPlayer.StateMachine.CurrentState;
        return state == _combatPlayer.IdleState || state == _combatPlayer.MoveState;
    }

    private void OpenPotionPage()
    {
        ResolvePotionFocus();
        if (_potionFocus == null)
        {
            Debug.LogWarning($"{name}: 물약 UI 연결이 없어 안내를 화면 중앙에 표시합니다.", this);
        }
        if (!PauseController.TryPauseForTutorial(this)) return;
        _potionStep = PotionStep.Explanation;
        _pageOpenedAt = Time.unscaledTime;
        if (_focusOverlay == null)
        {
            _focusOverlay = new Ep1TutorialFocusOverlay(transform);
        }
        RefreshPotionPage();
    }

    private void RefreshPotionPage()
    {
        if (_potionStep != PotionStep.Explanation || _focusOverlay == null) return;
        _focusOverlay.Show(_potionFocus, null, GetWorldCamera(), _potionExplanation, _continueMessage,
            _continueKeySprite, _font, _popupFontSize, _popupFontStyle, _popupTextColor, _popupOutlineColor,
            _popupOutlineThickness, _dimOpacity, _focusBorderColor, _focusPadding, _popupTextSize,
            _soulFocusPadding, _soulFocusMinimumSize);
    }

    private void AdvancePotionPage()
    {
        if (_focusOverlay != null)
        {
            _focusOverlay.Hide();
        }
        PauseController.ResumeFromTutorial(this);
        BeginPotionPractice();
    }

    private void BeginPotionPractice()
    {
        _potionStep = PotionStep.Practice;
        _potionUsedInPractice = false;
        EnsurePotionPracticeState();
        if (_potionHeadHint == null)
        {
            GameObject prefab = Resources.Load<GameObject>("Ep1RespawnHint");
            if (prefab != null)
            {
                _potionHeadHint = Instantiate(prefab, transform, false).GetComponent<Ep1HighJumpHint>();
            }
        }
        if (_potionHeadHint != null)
        {
            _potionHeadHint.Configure(this, Ep1TutorialHintType.PotionUse);
        }
    }

    /// <summary>세이브하면 체력이 가득 차서 물약을 못 마시므로, 연습 동안 체력을 깎아 두고 물약을 채워 둠</summary>
    private void EnsurePotionPracticeState()
    {
        if (_potionHealth.IsDead) return;
        if (_potionHealth.PotionCount <= 0)
        {
            _potionHealth.RefillPotions();
        }
        float target = _potionHealth.MaxHealth * _potionPracticeHealthRatio;
        if (_potionHealth.CurrentHealth > target)
        {
            _potionHealth.SetHealthForTutorial(target);
        }
    }

    private void HandleTutorialPotionUsed()
    {
        if (_potionStep == PotionStep.Practice)
        {
            _potionUsedInPractice = true;
        }
    }

    private void CompletePotionTutorial()
    {
        CompleteHint(Ep1TutorialHintType.PotionUse);
        TryMarkHintShown(Ep1TutorialHintType.PotionUse);
        _potionStep = PotionStep.Complete;
        // 연습으로 쓴 물약은 돌려줌 (세이브 직후 상태 그대로)
        if (!_potionHealth.IsDead)
        {
            _potionHealth.RefillPotions();
        }
        if (_potionHeadHint != null)
        {
            _potionHeadHint.FadeOutAndDestroy();
        }
        _potionHeadHint = null;
    }

    private void CancelPotionTutorial()
    {
        if (_potionStep == PotionStep.Explanation)
        {
            if (_focusOverlay != null)
            {
                _focusOverlay.Hide();
            }
            PauseController.ResumeFromTutorial(this);
        }
        if (_potionHeadHint != null)
        {
            _potionHeadHint.FadeOutAndDestroy();
        }
        _potionHeadHint = null;
        _potionUsedInPractice = false;
        _potionWaitElapsed = 0f;
        if (_potionStep != PotionStep.Complete)
        {
            _potionStep = PotionStep.WaitingForSave;
        }
    }

    private void ResolvePotionFocus()
    {
        if (_potionFocus != null) return;
        UI_PotionCount count = FindFirstObjectByType<UI_PotionCount>();
        if (count == null) return;
        // 개수 텍스트의 부모(물약 아이콘 + 개수 + 쿨타임)를 통째로 강조
        _potionFocus = (count.transform.parent != null ? count.transform.parent : count.transform) as RectTransform;
    }

    private void ValidatePotionSettings()
    {
        _potionTutorialDelay = Mathf.Max(0f, _potionTutorialDelay);
        _potionPracticeHealthRatio = Mathf.Clamp(_potionPracticeHealthRatio, 0.05f, 0.95f);
    }
}
