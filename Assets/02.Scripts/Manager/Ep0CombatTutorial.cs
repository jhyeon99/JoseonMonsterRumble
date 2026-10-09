using System.Collections;
using Com.LuisPedroFonseca.ProCamera2D;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class Ep0CombatTutorial : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private Player _player;
    [SerializeField] private Enemy _enemy;
    [SerializeField] private Transform _startPoint;
    [SerializeField] private PrologueIntroController _prologue;
    [SerializeField] private ProCamera2D _camera;

    [Header("Encounter")]
    [SerializeField, Min(0.5f)] private float _encounterDistance = 2.8f;
    [SerializeField] private bool _snapEnemyToGround = true;
    [SerializeField, Min(0.1f)] private float _noticeDuration = 1.2f;
    [SerializeField, Min(0.1f)] private float _dialogueDuration = 1.2f;

    [Header("Parry camera and timing")]
    [SerializeField, Min(0.5f)] private float _parryCameraSize = 5.5f;
    [SerializeField, Min(0.01f)] private float _zoomDuration = 0.35f;
    [SerializeField, Min(0.05f)] private float _parrySuccessDuration = 0.25f;

    [Header("Editable UI in the prefab")]
    [SerializeField] private GameObject _panel;
    [SerializeField] private Text _dialogue;
    [SerializeField] private Text _instruction;
    [SerializeField] private GameObject _keyPrompt;
    [SerializeField] private Text _keyLabel;
    [SerializeField] private Image _keyImage;
    [SerializeField] private Sprite _attackKeySprite;
    [SerializeField] private Sprite _parryKeySprite;

    [Header("Dialogue")]
    [SerializeField, TextArea] private string _encounterText = "헉스, 얜 뭐야?";
    [SerializeField, TextArea] private string _comboText = "일단 공격해보자.";
    [SerializeField, TextArea] private string _parryText = "공격을 막아볼까?";
    [SerializeField, TextArea] private string _parryTimingText = "공격이 닿기 직전에 막아보자.";
    [SerializeField, TextArea] private string _retryText = "한 번 더 막아보자.";
    [SerializeField, TextArea] private string _finisherText = "지금이다!";
    [SerializeField, TextArea] private string _completeText = "좋아. 계속 가보자.";

    private PlayerInputHandler _input;
    private Ep0TutorialMeleeCombat _combat;
    private HitStop _hitStop;
    private bool _initialized;
    private bool _enemyReady;
    private bool _enemyBodyWasSimulated;
    private bool _frozen;
    private bool _playerWasEnabled;
    private bool _inputWasEnabled;
    private bool _normalInputWasEnabled;
    private bool _cameraWasEnabled;
    private bool _hitStopWasEnabled;
    private float _savedTimeScale;
    private float _normalCameraSize;
    private bool _hasCameraSize;
    private int _comboStep;
    private bool _parried;
    private bool _hitPending;
    private bool _running;
    private bool _complete;

    public string Phase { get; private set; } = "Waiting";

    private sealed class WaitingState : EnemyState
    {
        private readonly Enemy _enemy;

        public WaitingState(Enemy enemy)
        {
            _enemy = enemy;
        }

        public override void Enter()
        {
            _enemy.MoveType.ResetLightKnockback();
            _enemy.MoveType.StopMove();
            _enemy.DirectionLock?.LockDirection();
            _enemy.Animation.SetRunning(false);
        }

        public override void FixedUpdate()
        {
            _enemy.MoveType.StopMove();
        }
    }

    private void OnEnable()
    {
        GameEvents.OnParry += HandleParry;
        if (_initialized && _enemy != null) _enemy.Health.OnHit += HandleEnemyHit;
        if (_initialized && _combat != null) _combat.OnHitDeferred += HandleHitDeferred;
    }

    private IEnumerator Start()
    {
        // Enemy.Start and Player.Start initialize their state machines first.
        yield return null;
        if (_player == null) _player = FindFirstObjectByType<Player>();
        if (_camera == null && Camera.main != null) _camera = Camera.main.GetComponent<ProCamera2D>();
        if (_prologue == null) _prologue = FindFirstObjectByType<PrologueIntroController>();
        if (_startPoint == null)
        {
            var point = GameObject.Find("SpawnPoint_02");
            if (point != null) _startPoint = point.transform;
        }

        if (_player == null || _enemy == null || _camera == null || _panel == null ||
            _dialogue == null || _instruction == null || _keyPrompt == null || _keyLabel == null ||
            !_enemy.TryGetComponent(out _combat) || !(_enemy.Health is Ep0TutorialEnemyHealth))
        {
            Debug.LogError("EP0 Tutorial: 플레이어, 근접 몬스터, ProCamera2D와 UI 연결을 확인하세요.", this);
            enabled = false;
            yield break;
        }

        _input = _player.GetComponent<PlayerInputHandler>();
        _combat.OnHitDeferred += HandleHitDeferred;
        _dialogue.alignment = TextAnchor.MiddleCenter;
        _instruction.alignment = TextAnchor.MiddleCenter;
        _keyLabel.alignment = TextAnchor.MiddleCenter;
        CopyOpeningDialogueStyle();
        // Use one centered text block, just like the opening sound caption.
        RectTransform textRect = _dialogue.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(40f, 28f);
        textRect.offsetMax = new Vector2(-40f, -28f);
        _instruction.gameObject.SetActive(false);
        _hitStop = FindFirstObjectByType<HitStop>();
        foreach (EnemyContactDamage contact in _enemy.GetComponentsInChildren<EnemyContactDamage>())
            contact.enabled = false;
        _enemy.Health.OnHit += HandleEnemyHit;
        _initialized = true;
        _enemyBodyWasSimulated = _enemy.EnemyRb.simulated;
        // Location 2 can be inactive during the opening; keep the enemy from falling through it.
        _enemy.EnemyRb.simulated = false;
        _enemy.StateMachine.ChangeState(new WaitingState(_enemy));
        _panel.SetActive(false);
    }

    private void Update()
    {
        if (!_initialized || _running || _complete || PauseController.IsPaused || _player.Health.IsDead) return;
        // The second location must be visible and player control restored before an encounter.
        if (_prologue != null && _prologue.Phase != "Complete") return;
        if (!_player.enabled || !_input.enabled || !_player.IsGrounded) return;
        if (_startPoint != null && Mathf.Abs(_player.transform.position.x - _startPoint.position.x) > 35f) return;

        if (!_enemyReady)
        {
            _enemy.EnemyRb.simulated = _enemyBodyWasSimulated;
            Physics2D.SyncTransforms();
            if (_snapEnemyToGround) SnapEnemyToGround();
            _enemyReady = true;
        }
        Vector2 playerPosition = _player.transform.position;
        Vector2 enemyPosition = _enemy.transform.position;
        if (Mathf.Abs(playerPosition.x - enemyPosition.x) > _encounterDistance ||
            Mathf.Abs(playerPosition.y - enemyPosition.y) > 4f)
        {
            return;
        }

        _running = true;
        StartCoroutine(RunTutorial());
    }

    private void LateUpdate()
    {
        if (_frozen) Time.timeScale = 0f;
        if (_running && _player != null && _player.Health.IsDead)
        {
            StopAllCoroutines();
            ReleaseControl();
            _running = false;
            Phase = "Waiting";
            _enemy.StateMachine.ChangeState(new WaitingState(_enemy));
        }
    }

    private IEnumerator RunTutorial()
    {
        _normalCameraSize = _camera.ScreenSizeInWorldCoordinates.y * 0.5f;
        _hasCameraSize = true;
        _parried = false;
        _normalInputWasEnabled = _input.enabled;
        CopyOpeningDialogueStyle();
        _player.RangedAttack.CancelCharge();
        _player.StateMachine.ChangeState(_player.IdleState);
        SetPlayerControl(false);
        FaceEachOther();

        Phase = "Encounter";
        Freeze();
        Show(_encounterText, "", "");
        yield return WaitRealtime(_noticeDuration);
        Unfreeze();

        Phase = "Combo";
        _comboStep = 0;
        SetPlayerControl(true);
        Show(_comboText, "X를 연속으로 눌러 3연속 공격  ·  0 / 3", "X");
        while (_comboStep < 3) yield return null;
        SetPlayerControl(false);
        while (_player.StateMachine.CurrentState == _player.AttackState) yield return null;

        Phase = "ParryExplanation";
        _player.StateMachine.ChangeState(_player.IdleState);
        FaceEachOther();
        Freeze();
        Show(_parryText, "", "");
        yield return WaitRealtime(_dialogueDuration);
        Unfreeze();

        while (!_parried)
        {
            yield return WaitForNeutralPlayer();
            FaceEachOther();
            _enemy.StateMachine.ChangeState(new WaitingState(_enemy));
            _combat.StopAttack();
            Phase = "Windup";
            Show("", "몬스터의 공격을 지켜보세요", "");
            _enemy.Animation.PlayAttack();
            yield return new WaitForSeconds(Mathf.Max(0.05f, _enemy.Data.AttackPrepareTime));
            _hitPending = false;
            _combat.DeferNextPlayerHit();
            _combat.Attack();

            // Freeze on the real physics contact, not on an estimated animation delay.
            float remaining = _enemy.Data.AttackDurationTime + 0.2f;
            while (!_hitPending && remaining > 0f)
            {
                remaining -= Time.deltaTime;
                yield return null;
            }
            if (_hitPending)
            {
                yield return ZoomTo(_parryCameraSize, GetFocusPosition());
                yield return WaitForKey(KeyCode.DownArrow);
                SetPlayerControl(true);
                Unfreeze();
                Phase = "ParryAttempt";
                _player.StateMachine.ChangeState(_player.GuardState);
                // The prompt accepted a fresh real press. Open the ordinary guard window now.
                _player.Guard.BeginGuard(true);
                // Resolve in this same frame: a tap cannot expire before the hit is checked.
                _combat.ResolveDeferredHit();
            }
            _combat.StopAttack();
            if (!_parried)
            {
                SetPlayerControl(false);
                Show(_retryText, "공격이 닿는 순간 ↓를 눌러 막아보세요", "↓");
                yield return new WaitForSeconds(0.7f);
            }
        }

        Phase = "ParrySuccess";
        _enemy.TryHoldGroggy();
        SetPlayerControl(false);
        Show("막았다!", "", "");
        yield return new WaitForSeconds(_parrySuccessDuration);

        Phase = "FinisherPrompt";
        Freeze();
        Show(_finisherText, "패링으로 빈틈이 생긴 적에게 X로 마무리 공격", "X");
        yield return WaitForKey(KeyCode.X);
        SetPlayerControl(true);
        Unfreeze();
        Phase = "Finisher";
        Show("", "가까이 다가가 X로 마무리 공격", "X");
        // Use the real target detection and finisher, rather than launching on a key press.
        _player.TryEnterFinisher();
        while (_enemy != null && !_enemy.IsDefeated) yield return null;
        _complete = true;
        Show(_completeText, "", "");
        yield return new WaitForSeconds(0.8f);
        Freeze();
        yield return ZoomTo(_normalCameraSize, GetPlayerCameraPosition());
        ReleaseControl();
        _running = false;
        Phase = "Complete";
    }

    private IEnumerator WaitForNeutralPlayer()
    {
        SetPlayerControl(false);
        while (!_player.IsGrounded || _player.StateMachine.CurrentState == _player.AttackState ||
               _player.StateMachine.CurrentState == _player.HitState)
        {
            yield return null;
        }

        _player.StateMachine.ChangeState(_player.IdleState);
    }

    private IEnumerator WaitForKey(KeyCode key)
    {
        while (Input.GetKey(key)) yield return null;
        while (PauseController.IsPaused || !Input.GetKeyDown(key)) yield return null;
    }

    private IEnumerator WaitRealtime(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!PauseController.IsPaused) elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void SetPlayerControl(bool allowed)
    {
        ClearPlayerInput();
        if (_frozen) _inputWasEnabled = allowed;
        else _input.enabled = allowed;
    }

    private void ClearPlayerInput()
    {
        _input.ClearInputState();
        // The original ClearInputState does not clear the guard buffer.
        _input.TryUseGuardInput();
    }

    private void CopyOpeningDialogueStyle()
    {
        // Read the opening's existing UI, including inactive panels, without modifying its script.
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas.name != "PrologueDialogue") continue;
            Transform source = canvas.transform.Find("DialoguePanel");
            if (source == null) continue;
            RectTransform from = source.GetComponent<RectTransform>();
            RectTransform to = _panel.GetComponent<RectTransform>();
            to.anchorMin = from.anchorMin;
            to.anchorMax = from.anchorMax;
            to.pivot = from.pivot;
            to.offsetMin = from.offsetMin;
            to.offsetMax = from.offsetMax;
            Image fromImage = source.GetComponent<Image>();
            Image toImage = _panel.GetComponent<Image>();
            toImage.sprite = fromImage.sprite;
            toImage.color = fromImage.color;
            toImage.type = fromImage.type;
            Outline fromOutline = source.GetComponent<Outline>();
            if (fromOutline != null)
            {
                Outline toOutline = _panel.GetComponent<Outline>();
                if (toOutline == null) toOutline = _panel.AddComponent<Outline>();
                toOutline.effectColor = fromOutline.effectColor;
                toOutline.effectDistance = fromOutline.effectDistance;
                toOutline.enabled = fromOutline.enabled;
            }
            Text subtitle = source.Find("Subtitle").GetComponent<Text>();
            foreach (Text label in new[] { _dialogue, _instruction })
            {
                label.font = subtitle.font;
                label.fontSize = subtitle.fontSize;
                label.fontStyle = subtitle.fontStyle;
                label.lineSpacing = subtitle.lineSpacing;
                label.color = subtitle.color;
                label.alignment = TextAnchor.MiddleCenter;
            }
            return;
        }
    }

    private void Freeze()
    {
        if (_frozen) return;
        _savedTimeScale = HitStop.IsActive ? 1f : Time.timeScale;
        HitStop.Stop();
        _playerWasEnabled = _player.enabled;
        _inputWasEnabled = _input.enabled;
        _cameraWasEnabled = _camera.enabled;
        _hitStopWasEnabled = _hitStop != null && _hitStop.enabled;
        if (_hitStop != null) _hitStop.enabled = false;
        ClearPlayerInput();
        _input.enabled = false;
        _player.enabled = false;
        _camera.enabled = false;
        _frozen = true;
        Time.timeScale = 0f;
    }

    private void Unfreeze()
    {
        if (!_frozen) return;
        _frozen = false;
        ClearPlayerInput();
        _input.enabled = _inputWasEnabled;
        _player.enabled = _playerWasEnabled;
        _camera.enabled = _cameraWasEnabled;
        if (_hitStop != null) _hitStop.enabled = _hitStopWasEnabled;
        Time.timeScale = PauseController.IsPaused ? 0f : Mathf.Max(0.01f, _savedTimeScale);
    }

    private IEnumerator ZoomTo(float size, Vector2 position)
    {
        float initialSize = _camera.ScreenSizeInWorldCoordinates.y * 0.5f;
        Vector2 initialPosition = _camera.transform.position;
        float elapsed = 0f;
        while (elapsed < _zoomDuration)
        {
            if (!PauseController.IsPaused) elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / _zoomDuration));
            _camera.UpdateScreenSize(Mathf.Lerp(initialSize, size, progress));
            var worldPosition = Vector2.Lerp(initialPosition, position, progress);
            Vector3 localPosition = _camera.transform.parent == null
                ? (Vector3)worldPosition
                : _camera.transform.parent.InverseTransformPoint(worldPosition);
            _camera.MoveCameraInstantlyToPosition(localPosition);
            yield return null;
        }
    }

    private Vector2 GetFocusPosition()
    {
        return ((Vector2)_player.transform.position + _enemy.GetEnemyCenterPosition()) * 0.5f + Vector2.up;
    }

    private Vector2 GetPlayerCameraPosition()
    {
        return (Vector2)_player.transform.position + new Vector2(_camera.OffsetX, _camera.OffsetY);
    }

    private void FaceEachOther()
    {
        float direction = _enemy.transform.position.x >= _player.transform.position.x ? 1f : -1f;
        Vector3 playerScale = _player.transform.localScale;
        playerScale.x = Mathf.Abs(playerScale.x) * direction;
        _player.transform.localScale = playerScale;
        Vector3 enemyScale = _enemy.transform.localScale;
        enemyScale.x = -Mathf.Abs(enemyScale.x) * direction;
        _enemy.transform.localScale = enemyScale;
    }

    private void SnapEnemyToGround()
    {
        Collider2D body = _enemy.GetComponent<Collider2D>();
        RaycastHit2D hit = Physics2D.Raycast((Vector2)_enemy.transform.position + Vector2.up * 12f,
            Vector2.down, 30f, Constants.Layers.GroundAllMask);
        if (hit.collider == null || hit.collider.isTrigger || body == null) return;
        float adjustment = hit.point.y - body.bounds.min.y + 0.02f;
        Vector2 position = _enemy.EnemyRb.position + Vector2.up * adjustment;
        _enemy.EnemyRb.position = position;
        _enemy.transform.position = new Vector3(position.x, position.y, _enemy.transform.position.z);
        _enemy.EnemyRb.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        _snapEnemyToGround = false;
    }

    private void HandleEnemyHit(HitData hitData)
    {
        if (Phase != "Combo" || hitData.ComboStep < 1 || hitData.ComboStep > 3) return;
        if (hitData.ComboStep == 1) _comboStep = 1;
        else if (hitData.ComboStep == _comboStep + 1) _comboStep++;
        else _comboStep = 0;
        Show(_comboText, $"X를 연속으로 눌러 3연속 공격  ·  {_comboStep} / 3", "X");
    }

    private void HandleHitDeferred()
    {
        if (!_running || Phase != "Windup") return;
        _hitPending = true;
        Phase = "ParryPrompt";
        Freeze();
        Show(_parryTimingText, "지금 ↓를 눌러 패링", "↓");
    }

    private void HandleParry(HitData hitData)
    {
        if (Phase != "ParryAttempt" || !ReferenceEquals(hitData.Source, _combat)) return;
        _parried = true;
        _enemy.TryHoldGroggy();
    }

    private void Show(string dialogue, string instruction, string key)
    {
        _panel.SetActive(true);
        _dialogue.text = string.IsNullOrEmpty(dialogue) ? instruction :
            string.IsNullOrEmpty(instruction) ? dialogue : dialogue + "\n" + instruction;
        _instruction.text = "";
        _keyPrompt.SetActive(!string.IsNullOrEmpty(key));
        Sprite sprite = key == "X" ? _attackKeySprite : _parryKeySprite;
        if (_keyImage != null)
        {
            _keyImage.sprite = sprite;
            _keyImage.color = sprite == null ? new Color(0.13f, 0.12f, 0.1f, 1f) : Color.white;
            _keyImage.preserveAspect = sprite != null;
        }
        _keyLabel.text = key;
        _keyLabel.gameObject.SetActive(sprite == null);
    }

    private void ReleaseControl()
    {
        Unfreeze();
        if (_combat != null) _combat.StopAttack();
        if (_input != null)
        {
            if (_running) SetPlayerControl(_normalInputWasEnabled);
            ClearPlayerInput();
        }
        if (_initialized && _enemy != null)
        {
            _enemy.ReleaseGroggy();
        }
        if (_hasCameraSize && _camera != null)
        {
            _camera.UpdateScreenSize(_normalCameraSize);
            _camera.Reset(true, false, false);
        }
        _hasCameraSize = false;
        if (_panel != null) _panel.SetActive(false);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        GameEvents.OnParry -= HandleParry;
        if (_combat != null) _combat.OnHitDeferred -= HandleHitDeferred;
        if (_initialized && _enemy != null) _enemy.Health.OnHit -= HandleEnemyHit;
        ReleaseControl();
        if (_initialized && _enemy != null && !_enemy.IsDefeated)
        {
            _enemy.EnemyRb.simulated = _enemyBodyWasSimulated;
            _enemy.DirectionLock?.UnlockDirection();
            _enemy.StateMachine.ChangeState(new WaitingState(_enemy));
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_enemy == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_enemy.transform.position, _encounterDistance);
    }
}
