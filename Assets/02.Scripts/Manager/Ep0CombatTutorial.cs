using System.Collections;
using System.Collections.Generic;
using Com.LuisPedroFonseca.ProCamera2D;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class Ep0CombatTutorial : MonoBehaviour
{
    private const int RequiredAttackHits = 3;
    private const float FleeEndX = 294f;

    [Header("Scene references")]
    [SerializeField] private Player _player;
    [SerializeField] private Enemy _enemy;
    [SerializeField] private Transform _startPoint;
    [SerializeField] private PrologueIntroController _prologue;
    [SerializeField] private ProCamera2D _camera;

    [Header("Encounter")]
    [SerializeField] private bool _beginAtLocation2Spawn = true;
    [SerializeField, Min(0.5f)] private float _encounterDistance = 2.8f;
    [SerializeField] private bool _snapEnemyToGround = true;
    [SerializeField, Min(0.1f)] private float _noticeDuration = 1.2f;
    [SerializeField, Min(0.1f)] private float _dialogueDuration = 1.2f;

    [Header("Monster camera presentation")]
    [SerializeField, Range(0.2f, 1f)] private float _monsterZoomRatio = 0.7f;
    [SerializeField, Min(0f)] private float _monsterFocusDuration = 0.6f;

    [Header("Camera and timing")]
    [SerializeField, Min(0.01f)] private float _zoomDuration = 0.35f;

    [Header("Player-side hint")]
    [SerializeField] private Vector2 _hintOffset = new Vector2(-220f, 80f);
    [SerializeField] private Vector2 _hintSize = new Vector2(340f, 100f);
    [SerializeField, Min(12)] private int _hintFontSize = 24;
    [SerializeField] private Color _hintTextColor = Color.black;
    [SerializeField] private Color _hintOutlineColor = Color.white;

    [Header("Editable UI in the prefab")]
    [SerializeField] private GameObject _panel;
    [SerializeField] private Text _dialogue;
    [SerializeField] private Text _instruction;
    [SerializeField] private GameObject _keyPrompt;
    [SerializeField] private Text _keyLabel;
    [SerializeField] private Image _keyImage;
    [SerializeField] private Sprite _attackKeySprite;
    [SerializeField] private Sprite _dodgeKeySprite;
    [SerializeField] private Font _focusFont;

    [Header("Dialogue")]
    [SerializeField, TextArea] private string _encounterText = "요괴?";
    [SerializeField, TextArea] private string _attackText = "X를 눌러 일반 공격을 해보자.";
    [SerializeField, TextArea] private string _dodgeText = "요괴가 공격하려고 한다.\nZ를 누르면 뒤로 물러나며 회피할 수 있다.";
    [SerializeField, TextArea] private string _fleeText = "도망간다! 쫓아가자.";

    [Header("Dodge focus and escape")]
    [SerializeField, Min(0.01f)] private float _freezeBeforeHit = 0.08f;
    [SerializeField, Range(0f, 1f)] private float _focusOpacity = 0.7f;
    [SerializeField] private Color _focusBorderColor = Color.white;
    [SerializeField, Min(0f)] private float _focusPadding = 24f;
    [SerializeField, Min(1f)] private float _fleeDistance = 45f;
    [SerializeField, Min(1f)] private float _fleeSpeedMultiplier = 2f;

    private PlayerInputHandler _input;
    private EnemyCombat _combat;
    private bool _initialized;
    private bool _enemyReady;
    private bool _enemyBodyWasSimulated;
    private bool _normalInputWasEnabled;
    private ProCamera2DCinematics _cinematics;
    private Transform _monsterFocus;
    private float _normalCameraSize;
    private bool _hasCameraSize;
    private int _attackHitCount;
    private bool _tutorialPaused;
    private bool _dodgeKeyReleased;
    private bool _dodgeTriggered;
    private Ep1TutorialFocusOverlay _focusOverlay;
    private SpriteRenderer _enemySprite;
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

    // ChaseState ancestry lets the existing ground movement apply its walk animation
    // curves. Override every AI callback so this enemy only escapes to the right.
    private sealed class FleeState : ChaseState
    {
        private readonly Enemy _enemy;
        private readonly float _speedMultiplier;

        public FleeState(Enemy enemy, float speedMultiplier) : base(enemy)
        {
            _enemy = enemy;
            _speedMultiplier = speedMultiplier;
        }

        public override void Enter()
        {
            _enemy.Combat.StopAttack();
            _enemy.MoveType.ResetLightKnockback();
            _enemy.MoveType.StopMove();
            _enemy.DirectionLock?.LockDirection();
            Vector3 scale = _enemy.transform.localScale;
            scale.x = Mathf.Abs(scale.x);
            _enemy.transform.localScale = scale;
            _enemy.Animation.SetAnimationSpeed(_speedMultiplier);
            _enemy.Animation.SetRunning(true);
        }

        public override void Update() { }

        public override void FixedUpdate()
        {
            if (_enemy.transform.position.x >= FleeEndX)
            {
                _enemy.MoveType.StopMove();
                Vector3 position = _enemy.transform.position;
                position.x = FleeEndX;
                _enemy.EnemyRb.position = new Vector2(position.x, position.y);
                _enemy.transform.position = position;
                _enemy.Animation.SetRunning(false);
                _enemy.Animation.ResetAnimationSpeed();
                _enemy.gameObject.SetActive(false);
                return;
            }

            _enemy.MoveType.Move(Vector2.right * _speedMultiplier);
        }

        public override void Exit()
        {
            _enemy.MoveType.StopMove();
            _enemy.Animation.SetRunning(false);
            _enemy.Animation.ResetAnimationSpeed();
        }
    }

    private void Awake()
    {
        // Direct scene play bypasses the bootstrap scene. Enemy.Start needs this manager
        // before it can register itself and bind the player used by native attack hit data.
        if (EnemyManager.Instance == null && FindFirstObjectByType<EnemyManager>() == null)
        {
            var managerObject = new GameObject("EnemyManager");
            managerObject.AddComponent<EnemyManager>();
        }

        // Hide the authored legacy panel even if a missing reference prevents initialization.
        if (_panel != null) _panel.SetActive(false);
    }

    private void OnEnable()
    {
        Canvas.preWillRenderCanvases += UpdateHintPosition;
        if (_initialized && _enemy != null) _enemy.Health.OnHit += HandleEnemyHit;
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

        if (_enemy == null) ResolveTutorialEnemy();
        if (_enemy == null)
        {
            Debug.LogError("EP0 Tutorial: 튜토리얼 몬스터가 삭제되었거나 Enemy 연결이 비어 있습니다. PF_Ep0CombatTutorial 안의 TutorialMonster를 복구하고 Enemy에 연결하세요. 일반 몬스터는 연습용 몬스터를 대신할 수 없습니다.", this);
            enabled = false;
            yield break;
        }

        if (_player == null || _enemy == null || _camera == null || _panel == null ||
            _dialogue == null || _instruction == null || _keyPrompt == null || _keyLabel == null ||
            !_enemy.TryGetComponent(out _combat) || !(_enemy.Health is Ep0TutorialEnemyHealth))
        {
            Debug.LogError("EP0 Tutorial: 플레이어, 근접 몬스터, ProCamera2D와 UI 연결을 확인하세요.", this);
            enabled = false;
            yield break;
        }

        _cinematics = _camera.GetComponent<ProCamera2DCinematics>();
        if (_cinematics == null || !_cinematics.enabled)
        {
            Debug.LogError("EP0 Tutorial: Main Camera의 ProCamera2D Cinematics를 활성화하세요.", this);
            enabled = false;
            yield break;
        }
        _monsterFocus = new GameObject("TutorialMonsterFocus").transform;
        _monsterFocus.SetParent(transform, false);
        _input = _player.GetComponent<PlayerInputHandler>();
        _enemySprite = _enemy.GetComponentInChildren<SpriteRenderer>();
        _focusOverlay = new Ep1TutorialFocusOverlay(transform);
        _dialogue.alignment = TextAnchor.MiddleCenter;
        _instruction.alignment = TextAnchor.MiddleCenter;
        _keyLabel.alignment = TextAnchor.MiddleCenter;
        ConfigurePlayerHint();
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

    private void ResolveTutorialEnemy()
    {
        // Recover an unassigned reference without selecting unrelated normal monsters.
        Enemy[] children = GetComponentsInChildren<Enemy>(true);
        foreach (Enemy candidate in children)
        {
            if (candidate.GetComponent<Ep0TutorialEnemyHealth>() == null ||
                candidate.GetComponent<EnemyCombat>() == null)
            {
                continue;
            }

            _enemy = candidate;
            return;
        }

        Enemy match = null;
        Ep0TutorialEnemyHealth[] healths = FindObjectsByType<Ep0TutorialEnemyHealth>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Ep0TutorialEnemyHealth health in healths)
        {
            if (health.gameObject.scene != gameObject.scene) continue;
            Enemy candidate = health.GetComponent<Enemy>();
            if (candidate == null || candidate.GetComponent<EnemyCombat>() == null) continue;
            if (match != null) return;
            match = candidate;
        }
        _enemy = match;
    }

    private void Update()
    {
        if (_tutorialPaused && Phase == "DodgeFocus")
        {
            if (!Input.GetKey(KeyCode.Z)) _dodgeKeyReleased = true;
            if (_dodgeKeyReleased && Input.GetKeyDown(KeyCode.Z)) ResumeWithDodge();
            return;
        }
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
        if (!_beginAtLocation2Spawn &&
            (Mathf.Abs(playerPosition.x - enemyPosition.x) > _encounterDistance ||
             Mathf.Abs(playerPosition.y - enemyPosition.y) > 4f))
        {
            return;
        }

        _running = true;
        StartCoroutine(RunTutorial());
    }

    private void LateUpdate()
    {
        UpdateHintPosition();
        KeepPlayerBeforeTutorialEnemy();
        if (_running && _player != null && _player.Health.IsDead)
        {
            StopAllCoroutines();
            ReleaseControl();
            _running = false;
            Phase = "Waiting";
            _enemy.StateMachine.ChangeState(new WaitingState(_enemy));
        }
    }

    private void KeepPlayerBeforeTutorialEnemy()
    {
        if (!_initialized || _attackHitCount >= RequiredAttackHits || _player.Health.IsDead ||
            !_enemy.gameObject.activeInHierarchy || (_prologue != null && !_prologue.IsIntroComplete))
        {
            return;
        }

        // This horizontal limit also blocks jumping or dodging past the practice enemy.
        float limitX = _enemy.transform.position.x;
        Collider2D enemyCollider = _enemy.GetComponent<Collider2D>();
        Collider2D playerCollider = _player.GetComponent<Collider2D>();
        if (enemyCollider != null && playerCollider != null)
        {
            float playerRightOffset = playerCollider.bounds.max.x - _player.transform.position.x;
            limitX = enemyCollider.bounds.min.x - playerRightOffset;
        }
        if (_player.transform.position.x <= limitX) return;

        Vector3 position = _player.transform.position;
        position.x = limitX;
        Rigidbody2D body = _player.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.position = new Vector2(position.x, position.y);
            Vector2 velocity = body.linearVelocity;
            velocity.x = Mathf.Min(velocity.x, 0f);
            body.linearVelocity = velocity;
        }
        _player.transform.position = position;
    }

    private IEnumerator RunTutorial()
    {
        _normalCameraSize = _camera.ScreenSizeInWorldCoordinates.y * 0.5f;
        _hasCameraSize = true;
        _attackHitCount = 0;
        _dodgeTriggered = false;
        _normalInputWasEnabled = _input.enabled;
        Phase = "FirstMonsterFocus";
        _panel.SetActive(false);
        yield return ShowMonsterCinematic();

        Phase = "Encounter";
        Show(_encounterText, "", "");
        yield return WaitRealtime(_noticeDuration);
        Phase = "Attack";
        SetPlayerControl(true);
        _input.SetTutorialInputRestriction(this, GameplayInputActions.Movement | GameplayInputActions.Attack);
        ShowAttackProgress();
        while (_attackHitCount < RequiredAttackHits) yield return null;
        _input.SetTutorialInputRestriction(this, GameplayInputActions.Movement);
        SetPlayerControl(false);
        while (_player.StateMachine.CurrentState == _player.AttackState) yield return null;
        yield return WaitForNeutralPlayer();
        _input.ClearTutorialInputRestriction(this);

        SetPlayerControl(false);
        FaceEachOther();
        _enemy.StateMachine.ChangeState(new WaitingState(_enemy));
        _combat.StopAttack();
        _panel.SetActive(false);
        Phase = "Windup";
        _enemy.Animation.PlayAttack();
        // Freeze just before the hit, so resuming the native backstep avoids this attack.
        float remainingWindup = Mathf.Min(_freezeBeforeHit,
            _player.Movement.DodgeInvincibleDuration * 0.5f);
        yield return new WaitForSeconds(Mathf.Max(0.05f, _enemy.Data.AttackPrepareTime - remainingWindup));
        while (!PauseController.TryPauseForTutorial(this)) yield return null;
        _tutorialPaused = true;
        _dodgeKeyReleased = !Input.GetKey(KeyCode.Z);
        Phase = "DodgeFocus";
        ShowDodgeFocus();
        while (!_dodgeTriggered) yield return null;

        // The Z press changes state explicitly: ordinary buffered inputs are cleared during pause.
        yield return new WaitForSeconds(remainingWindup);
        _combat.IncreaseDamage(-_enemy.Data.MeleeAttackDamage);
        _combat.Attack();
        yield return new WaitForSeconds(_enemy.Data.AttackDurationTime);
        _combat.StopAttack();
        _combat.NormalDamage();
        while (_player.StateMachine.CurrentState == _player.DodgeState) yield return null;

        BeginEscape();
        _complete = true;
        Phase = "Chase";
        Show(_fleeText, "", "");
        yield return new WaitForSeconds(_dialogueDuration);
        ReleaseControl();
        _running = false;
        Phase = "Complete";
    }

    private void ShowDodgeFocus()
    {
        _focusOverlay.Show(null, null, _camera.GameCamera, _dodgeText, "Z를 눌러 회피", _dodgeKeySprite,
            _focusFont != null ? _focusFont : _dialogue.font,
            Mathf.Max(24, _hintFontSize), _dialogue.fontStyle, Color.white,
            Color.black, 0f, _focusOpacity, _focusBorderColor, 0f,
            new Vector2(440f, 100f), _focusPadding, new Vector2(120f, 120f), _enemySprite);
    }

    private void ResumeWithDodge()
    {
        if (!_tutorialPaused || Phase != "DodgeFocus" || _player.Health.IsDead) return;
        PauseController.ResumeFromTutorial(this);
        _tutorialPaused = false;
        _focusOverlay.Hide();
        SetPlayerControl(true);
        _player.StateMachine.ChangeState(_player.DodgeState);
        _dodgeTriggered = true;
        Phase = "Dodge";
    }

    private void BeginEscape()
    {
        _enemy.ReleaseGroggy();
        EnemyMoveArea area = _enemy.MoveArea;
        if (area != null)
        {
            float left = area.StartPosition.x + area.CenterOffset.x - area.AreaSize.x * 0.5f;
            float right = Mathf.Max(area.StartPosition.x + area.CenterOffset.x + area.AreaSize.x * 0.5f,
                Mathf.Max(_enemy.transform.position.x + _fleeDistance, FleeEndX + _fleeDistance));
            area.CenterOffset = new Vector2((left + right) * 0.5f - area.StartPosition.x, area.CenterOffset.y);
            area.AreaSize = new Vector2(right - left, area.AreaSize.y);
        }
        _enemy.StateMachine.ChangeState(new FleeState(_enemy, _fleeSpeedMultiplier));
    }

    private IEnumerator WaitForNeutralPlayer()
    {
        SetPlayerControl(true);
        while (!_player.IsGrounded || !_player.Movement.CanDodge ||
               _player.StateMachine.CurrentState == _player.DodgeState ||
               _player.StateMachine.CurrentState == _player.AttackState ||
               _player.StateMachine.CurrentState == _player.HitState)
        {
            yield return null;
        }

        _player.StateMachine.ChangeState(_player.IdleState);
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
        _input.enabled = allowed;
    }

    private void ClearPlayerInput()
    {
        _input.ClearInputState();
    }

    private void ConfigurePlayerHint()
    {
        Canvas canvas = _panel.GetComponentInParent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        // The scene currently overrides this canvas to scale zero and non-stretch anchors.
        canvasRect.localScale = Vector3.one;
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.pivot = new Vector2(0.5f, 0.5f);
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;
        // The monster guide has its own text/key-only root. Never reopen the dialogue panel.
        _panel.SetActive(false);
        var hintRoot = new GameObject("PlayerTutorialHint", typeof(RectTransform));
        hintRoot.transform.SetParent(_panel.transform.parent, false);
        _dialogue.transform.SetParent(hintRoot.transform, false);
        _keyPrompt.transform.SetParent(hintRoot.transform, false);
        _panel = hintRoot;
        RectTransform panelRect = _panel.GetComponent<RectTransform>();
        panelRect.localScale = Vector3.one;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = panelRect.anchorMin;
        panelRect.pivot = panelRect.anchorMin;
        panelRect.sizeDelta = _hintSize;
        Image background = _panel.GetComponent<Image>();
        if (background != null) background.enabled = false;
        Outline border = _panel.GetComponent<Outline>();
        if (border != null) border.enabled = false;
        _instruction.gameObject.SetActive(false);
        RectTransform textRect = _dialogue.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(4f, 4f);
        textRect.offsetMax = new Vector2(-84f, -4f);
        _dialogue.fontSize = _hintFontSize;
        _dialogue.color = _hintTextColor;
        _dialogue.raycastTarget = false;
        Outline textOutline = _dialogue.GetComponent<Outline>();
        if (textOutline == null) textOutline = _dialogue.gameObject.AddComponent<Outline>();
        textOutline.effectColor = _hintOutlineColor;
        textOutline.effectDistance = new Vector2(1f, -1f);
        RectTransform keyRect = _keyPrompt.GetComponent<RectTransform>();
        keyRect.anchorMin = new Vector2(1f, 0.5f);
        keyRect.anchorMax = keyRect.anchorMin;
        keyRect.pivot = new Vector2(0.5f, 0.5f);
        keyRect.anchoredPosition = new Vector2(-40f, 0f);
        keyRect.sizeDelta = new Vector2(64f, 44f);
        _keyLabel.fontSize = _hintFontSize;
    }

    private void UpdateHintPosition()
    {
        if (!_initialized || !_panel.activeSelf || _player == null || _camera == null) return;
        RectTransform panelRect = _panel.GetComponent<RectTransform>();
        TutorialHintPositioning.Position(panelRect, _camera.GetComponent<Camera>(),
            TutorialHintPositioning.GetRenderedPlayerAnchor(_player, false), _hintOffset, true);
    }

    private IEnumerator ShowMonsterCinematic()
    {
        while (_cinematics.IsPlaying) yield return null;
        SetPlayerControl(false);
        _player.RangedAttack?.CancelCharge();
        _player.StateMachine.ChangeState(_player.IdleState);
        Rigidbody2D body = _player.GetComponent<Rigidbody2D>();
        if (body != null) body.linearVelocity = Vector2.zero;
        // Start from the grounded player instead of an in-flight follow position.
        _camera.CenterOnTargets();
        yield return null;
        _monsterFocus.position = _enemy.GetEnemyCenterPosition();
        _cinematics.UseLetterbox = true;
        _cinematics.EndDuration = _zoomDuration;
        _cinematics.CinematicTargets = new List<CinematicTarget>
        {
            new CinematicTarget
            {
                TargetTransform = _monsterFocus,
                EaseInDuration = _zoomDuration,
                HoldDuration = _monsterFocusDuration,
                Zoom = 1f / _monsterZoomRatio,
                EaseType = EaseType.EaseInOut
            }
        };
        _cinematics.Play();
        while (_cinematics.IsPlaying) yield return null;
        SetPlayerControl(_normalInputWasEnabled);
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
        if (Phase != "Attack" || hitData.ComboStep < 1 || hitData.ComboStep > 3 ||
            _attackHitCount >= RequiredAttackHits)
        {
            return;
        }
        _attackHitCount++;
        ShowAttackProgress();
    }

    private void ShowAttackProgress() => Show(_attackText, $"일반 공격 {_attackHitCount}/{RequiredAttackHits}", "X");

    private void Show(string dialogue, string instruction, string key)
    {
        _panel.SetActive(true);
        UpdateHintPosition();
        _dialogue.text = string.IsNullOrEmpty(dialogue) ? instruction :
            string.IsNullOrEmpty(instruction) ? dialogue : dialogue + "\n" + instruction;
        _instruction.text = "";
        _keyPrompt.SetActive(!string.IsNullOrEmpty(key));
        Sprite sprite = key == "X" ? _attackKeySprite : key == "Z" ? _dodgeKeySprite : null;
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
        PauseController.ResumeFromTutorial(this);
        _tutorialPaused = false;
        _focusOverlay?.Hide();
        if (_running && _cinematics != null && _cinematics.IsPlaying) _cinematics.Stop();
        if (_combat != null)
        {
            _combat.StopAttack();
            _combat.NormalDamage();
        }
        if (_input != null)
        {
            _input.ClearTutorialInputRestriction(this);
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

    public void CancelForSceneExit()
    {
        StopAllCoroutines();
        ReleaseControl();
        _running = false;
        if (_initialized && _enemy != null && !_enemy.IsDefeated)
            _enemy.StateMachine.ChangeState(new WaitingState(_enemy));
    }

    private void OnDisable()
    {
        Canvas.preWillRenderCanvases -= UpdateHintPosition;
        StopAllCoroutines();
        if (_initialized && _enemy != null) _enemy.Health.OnHit -= HandleEnemyHit;
        ReleaseControl();
        _running = false;
        if (_initialized && _enemy != null && !_enemy.IsDefeated && !_complete)
        {
            _enemy.EnemyRb.simulated = _enemyBodyWasSimulated;
            _enemy.DirectionLock?.UnlockDirection();
            _enemy.StateMachine.ChangeState(new WaitingState(_enemy));
            Phase = "Waiting";
        }
    }

    private void OnDestroy() => _focusOverlay?.Dispose();

    private void OnDrawGizmosSelected()
    {
        if (_enemy == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_enemy.transform.position, _encounterDistance);
    }
}
