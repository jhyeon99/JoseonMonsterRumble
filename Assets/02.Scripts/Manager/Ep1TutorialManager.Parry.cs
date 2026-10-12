using UnityEngine;

public sealed partial class Ep1TutorialManager
{
    private enum ParryFocusStep { WaitingForSuccess, Pending, ZoomingEnemy, Explanation, ReturningCamera, Complete }

    private static bool _parryFinisherExplained;

    [SerializeField, TextArea(2, 5)]
    private string _parryFinisherMessage =
        "패링에 성공한 몬스터는 노랗게 반짝입니다.\nX를 눌러 마무리 공격이 가능합니다.";
    [SerializeField, Range(0.2f, 0.9f)] private float _parryZoomFactor = 0.45f;
    [SerializeField, Min(0.01f)] private float _parryZoomDuration = 0.5f;
    [SerializeField, Min(0.01f)] private float _parryReturnDuration = 0.4f;
    [SerializeField, Range(0f, 0.2f)] private float _parryLetterboxAmount = 0.08f;

    private ParryFocusStep _parryFocusStep;
    private Enemy _parriedTutorialEnemy;
    private SpriteRenderer _parryRenderer;
    private Ep1TutorialCameraFocus _parryCameraFocus;
    private GroggyState _parryGroggyState;
    private bool _parryBlinkWasUnscaled;

    private bool IsParryCinematicActive => _parryFocusStep == ParryFocusStep.ZoomingEnemy ||
        _parryFocusStep == ParryFocusStep.Explanation || _parryFocusStep == ParryFocusStep.ReturningCamera;

    private bool IsParryTutorialPending => _parryFocusStep == ParryFocusStep.Pending || IsParryCinematicActive;

    private void HandleTutorialParry(HitData hit)
    {
        if (_parryFinisherExplained || IsParryTutorialPending ||
            _combatStep != CombatStep.WaitingForFinisher)
        {
            return;
        }
        // Use the attack's real source, so another enemy or a reflected projectile cannot steal focus.
        var source = hit.Source as Component;
        Enemy enemy = source != null ? source.GetComponentInParent<Enemy>() : null;
        if (enemy == null || !enemy.IsGroggy || enemy.IsDefeated || enemy.Health == null || !enemy.Health.IsAlive) return;
        if (_hasCombatEnemySequence && enemy.Health != _boundPrerequisites[1]) return;
        _parriedTutorialEnemy = enemy;
        // Let the successful parry callback finish before taking ownership of pause and camera.
        _parryFocusStep = ParryFocusStep.Pending;
    }

    private bool UpdateParryTutorial()
    {
        if (!IsParryTutorialPending) return false;
        if (_parriedTutorialEnemy == null || !_parriedTutorialEnemy.isActiveAndEnabled ||
            !_parriedTutorialEnemy.IsGroggy || _parriedTutorialEnemy.IsDefeated ||
            !_parriedTutorialEnemy.Health.IsAlive || _combatStep != CombatStep.WaitingForFinisher)
        {
            CancelParryTutorial();
            return false;
        }
        if (_parryFocusStep == ParryFocusStep.Pending)
        {
            if (PauseController.IsGameplayInputBlocked || SceneArrivalFade.IsTransitioning || !_combatInput.enabled)
                return true;
            Camera camera = GetWorldCamera();
            Transform visual = _parriedTutorialEnemy.transform.Find("Visual");
            _parryRenderer = visual != null ? visual.GetComponentInChildren<SpriteRenderer>() : null;
            if (_parryRenderer == null || !_parryRenderer.enabled || _parryRenderer.sprite == null)
            {
                _parryRenderer = null;
                foreach (SpriteRenderer renderer in _parriedTutorialEnemy.GetComponentsInChildren<SpriteRenderer>())
                    if (renderer.enabled && renderer.sprite != null) { _parryRenderer = renderer; break; }
            }
            if (camera == null || !camera.orthographic || _parryRenderer == null)
            {
                CancelParryTutorial();
                return false;
            }
            if (!PauseController.TryPauseForTutorial(this)) return true;
            _parryGroggyState = _parriedTutorialEnemy.StateMachine.CurrentState as GroggyState;
            if (_parryGroggyState != null)
            {
                _parryBlinkWasUnscaled = _parryGroggyState.UseUnscaledBlink;
                _parryGroggyState.UseUnscaledBlink = true;
            }
            if (_parryCameraFocus == null) _parryCameraFocus = gameObject.AddComponent<Ep1TutorialCameraFocus>();
            _parryCameraFocus.Begin(camera, _parryRenderer.bounds,
                _parryZoomFactor, _parryZoomDuration, _parryLetterboxAmount);
            _parryFocusStep = ParryFocusStep.ZoomingEnemy;
        }
        else if (_parryFocusStep == ParryFocusStep.ZoomingEnemy && _parryCameraFocus.HasReachedTarget)
        {
            _parryFocusStep = ParryFocusStep.Explanation;
            _pageOpenedAt = Time.unscaledTime;
        }
        else if (_parryFocusStep == ParryFocusStep.Explanation &&
            Time.unscaledTime - _pageOpenedAt >= _enterDelay &&
            (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            AdvanceParryPage();
        }
        else if (_parryFocusStep == ParryFocusStep.ReturningCamera && _parryCameraFocus.HasReturned)
        {
            _parryFinisherExplained = true;
            _parryFocusStep = ParryFocusStep.Complete;
            RestoreParryBlink();
            _parriedTutorialEnemy = null;
            PauseController.ResumeFromTutorial(this);
        }
        return true;
    }

    private void AdvanceParryPage()
    {
        if (_parryFocusStep != ParryFocusStep.Explanation) return;
        if (_focusOverlay != null) _focusOverlay.Hide();
        _parryFocusStep = ParryFocusStep.ReturningCamera;
        _parryCameraFocus.Return(_parryReturnDuration);
    }

    private void RefreshParryPage()
    {
        if (_parryFocusStep != ParryFocusStep.Explanation) return;
        if (_focusOverlay == null) _focusOverlay = new Ep1TutorialFocusOverlay(transform);
        _focusOverlay.Show(null, null, GetWorldCamera(), _parryFinisherMessage, _continueMessage, _continueKeySprite,
            _font, _popupFontSize, _popupFontStyle, _popupTextColor, _popupOutlineColor,
            _popupOutlineThickness, Mathf.Min(_dimOpacity, 0.4f), _focusBorderColor, _focusPadding,
            _popupTextSize, 24f, new Vector2(240f, 180f), _parryRenderer);
    }

    private void RestoreParryBlink()
    {
        if (_parryGroggyState != null) _parryGroggyState.UseUnscaledBlink = _parryBlinkWasUnscaled;
        _parryGroggyState = null;
    }

    private void CancelParryTutorial()
    {
        bool wasActive = IsParryCinematicActive;
        if (_parryCameraFocus != null) _parryCameraFocus.Cancel();
        RestoreParryBlink();
        if (wasActive)
        {
            if (_focusOverlay != null) _focusOverlay.Hide();
            PauseController.ResumeFromTutorial(this);
        }
        _parriedTutorialEnemy = null;
        _parryRenderer = null;
        _parryFocusStep = _parryFinisherExplained ? ParryFocusStep.Complete : ParryFocusStep.WaitingForSuccess;
    }

    private void ValidateParrySettings()
    {
        _parryZoomFactor = Mathf.Clamp(_parryZoomFactor, 0.2f, 0.9f);
        _parryZoomDuration = Mathf.Max(0.01f, _parryZoomDuration);
        _parryReturnDuration = Mathf.Max(0.01f, _parryReturnDuration);
        _parryLetterboxAmount = Mathf.Clamp(_parryLetterboxAmount, 0f, 0.2f);
    }
}
