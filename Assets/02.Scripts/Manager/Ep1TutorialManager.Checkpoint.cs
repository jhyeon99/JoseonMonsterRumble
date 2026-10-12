using UnityEngine;

public sealed partial class Ep1TutorialManager
{
    private enum CheckpointStep { WaitingForStart, ZoomingCheckpoint, Explanation, ReturningCamera, WaitingForActivation, Complete }

    [SerializeField] private bool _enableCheckpointCinematic = true;
    [SerializeField, Min(0f)] private float _checkpointRevealDelay = 0.15f;
    [SerializeField, Range(0.2f, 0.9f)] private float _checkpointZoomFactor = 0.45f;
    [SerializeField, Min(0.01f)] private float _checkpointZoomDuration = 0.7f;
    [SerializeField, Min(0.01f)] private float _checkpointReturnDuration = 0.5f;
    [SerializeField, Range(0f, 0.2f)] private float _checkpointLetterboxAmount = 0.08f;

    private CheckpointStep _checkpointStep;
    private Ep1TutorialCameraFocus _checkpointCameraFocus;
    private SpriteRenderer _checkpointRenderer;
    private int _checkpointRevealedFrame = -1;
    private float _checkpointRevealedAt;

    private bool IsCheckpointCinematicActive => _checkpointStep == CheckpointStep.ZoomingCheckpoint ||
        _checkpointStep == CheckpointStep.Explanation || _checkpointStep == CheckpointStep.ReturningCamera;

    private bool IsCheckpointTutorialPending => _enableCheckpointCinematic && _respawnHintTarget != null &&
        _respawnHintTarget.isActiveAndEnabled && _checkpointStep != CheckpointStep.Complete;

    private bool IsCheckpointIntroductionPending => IsCheckpointTutorialPending &&
        _checkpointStep != CheckpointStep.WaitingForActivation;

    internal bool ShouldStartHighJumpHint => _enableCheckpointCinematic &&
        HasShownHint(Ep1TutorialHintType.Respawn) && !IsCheckpointCinematicActive &&
        _checkpointStep != CheckpointStep.WaitingForStart;

    private bool IsTutorialCheckpointActivated => StageManager.Instance != null &&
        StageManager.Instance.IsCheckpointActive(_respawnHintTarget);

    private void InitializeCheckpointTutorial()
    {
        _checkpointRevealedFrame = -1;
        _checkpointStep = !_enableCheckpointCinematic || _respawnHintTarget == null ||
            GetWorldCamera() == null || !GetWorldCamera().orthographic ? CheckpointStep.Complete :
            !HasShownHint(Ep1TutorialHintType.Respawn) ? CheckpointStep.WaitingForStart :
            IsTutorialCheckpointActivated ? CheckpointStep.Complete : CheckpointStep.WaitingForActivation;
    }

    private bool UpdateCheckpointTutorial()
    {
        if (!_enableCheckpointCinematic || _respawnHintTarget == null || !_respawnHintTarget.isActiveAndEnabled)
        {
            if (IsCheckpointCinematicActive) CancelCheckpointTutorial();
            return false;
        }
        if (_checkpointStep == CheckpointStep.Complete) return false;
        if (IsCheckpointCinematicActive)
        {
            if (_checkpointStep == CheckpointStep.ZoomingCheckpoint && _checkpointCameraFocus.HasReachedTarget)
            {
                _checkpointStep = CheckpointStep.Explanation;
                _pageOpenedAt = Time.unscaledTime;
            }
            else if (_checkpointStep == CheckpointStep.Explanation &&
                Time.unscaledTime - _pageOpenedAt >= _enterDelay &&
                (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                AdvanceCheckpointPage();
            }
            else if (_checkpointStep == CheckpointStep.ReturningCamera && _checkpointCameraFocus.HasReturned)
            {
                TryMarkHintShown(Ep1TutorialHintType.Respawn);
                _checkpointStep = IsTutorialCheckpointActivated ?
                    CheckpointStep.Complete : CheckpointStep.WaitingForActivation;
                PauseController.ResumeFromTutorial(this);
            }
            return true;
        }
        if (_checkpointStep == CheckpointStep.WaitingForActivation)
        {
            // Observe the real saved checkpoint; proximity alone must not unlock combat.
            if (IsTutorialCheckpointActivated) _checkpointStep = CheckpointStep.Complete;
            return false;
        }
        if (PauseController.IsGameplayInputBlocked || SceneArrivalFade.IsTransitioning || !_combatInput.enabled)
        {
            _checkpointRevealedFrame = -1;
            return false;
        }
        // Give the fully revealed scene a rendered frame before moving away from the player.
        if (_checkpointRevealedFrame < 0)
        {
            _checkpointRevealedFrame = Time.frameCount;
            _checkpointRevealedAt = Time.unscaledTime;
            return false;
        }
        if (Time.frameCount <= _checkpointRevealedFrame ||
            Time.unscaledTime - _checkpointRevealedAt < _checkpointRevealDelay)
        {
            return false;
        }

        if (!PauseController.TryPauseForTutorial(this)) return false;
        Vector3 checkpointPosition = _respawnHintTarget.transform.position;

        _checkpointRenderer = _respawnHintTarget.GetComponent<SpriteRenderer>();
        Bounds focus = _checkpointRenderer != null && _checkpointRenderer.sprite != null ?
            _checkpointRenderer.bounds : new Bounds(checkpointPosition + Vector3.up, new Vector3(1.5f, 2f, 0f));
        if (_checkpointCameraFocus == null) _checkpointCameraFocus = gameObject.AddComponent<Ep1TutorialCameraFocus>();
        _checkpointCameraFocus.Begin(GetWorldCamera(), focus,
            _checkpointZoomFactor, _checkpointZoomDuration, _checkpointLetterboxAmount);
        _checkpointStep = CheckpointStep.ZoomingCheckpoint;
        if (_focusOverlay == null) _focusOverlay = new Ep1TutorialFocusOverlay(transform);
        return true;
    }

    private void AdvanceCheckpointPage()
    {
        if (_checkpointStep != CheckpointStep.Explanation) return;
        if (_focusOverlay != null) _focusOverlay.Hide();
        _checkpointStep = CheckpointStep.ReturningCamera;
        _checkpointCameraFocus.Return(_checkpointReturnDuration);
    }

    private void RefreshCheckpointPage()
    {
        if (_checkpointStep != CheckpointStep.Explanation) return;
        if (_focusOverlay == null) _focusOverlay = new Ep1TutorialFocusOverlay(transform);
        _focusOverlay.Show(null, null, GetWorldCamera(), _respawnMessage, _continueMessage, _continueKeySprite,
            _font, _popupFontSize, _popupFontStyle, _popupTextColor, _popupOutlineColor,
            _popupOutlineThickness, Mathf.Min(_dimOpacity, 0.4f), _focusBorderColor, _focusPadding,
            _popupTextSize, 24f, new Vector2(240f, 180f), _checkpointRenderer);
    }

    private void CancelCheckpointTutorial()
    {
        bool wasActive = IsCheckpointCinematicActive;
        if (_checkpointCameraFocus != null) _checkpointCameraFocus.Cancel();
        if (wasActive)
        {
            if (_focusOverlay != null) _focusOverlay.Hide();
            PauseController.ResumeFromTutorial(this);
        }
        InitializeCheckpointTutorial();
    }

    private void ValidateCheckpointSettings()
    {
        _checkpointRevealDelay = Mathf.Max(0f, _checkpointRevealDelay);
        _checkpointZoomFactor = Mathf.Clamp(_checkpointZoomFactor, 0.2f, 0.9f);
        _checkpointZoomDuration = Mathf.Max(0.01f, _checkpointZoomDuration);
        _checkpointReturnDuration = Mathf.Max(0.01f, _checkpointReturnDuration);
        _checkpointLetterboxAmount = Mathf.Clamp(_checkpointLetterboxAmount, 0f, 0.2f);
    }
}
