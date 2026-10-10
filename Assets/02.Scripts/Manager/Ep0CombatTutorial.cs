using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] private Sprite _parryKeySprite;

    [Header("Dialogue")]
    [SerializeField, TextArea] private string _encounterText = "요괴?";
    [SerializeField, TextArea] private string _comboText = "일단 공격해보자.";
    [SerializeField, TextArea] private string _parryText = "공격을 막아볼까?";
    [SerializeField, TextArea] private string _parryTimingText = "공격이 닿기 직전에 막아보자.";
    [SerializeField, TextArea] private string _retryText = "한 번 더 막아보자.";
    [SerializeField, TextArea] private string _finisherText = "지금이다!";
    [SerializeField, TextArea] private string _completeText = "좋아. 계속 가보자.";

    [Header("Editable instructions")]
    [Tooltip("{count}는 현재 타격 수, {total}은 목표 타격 수로 표시됩니다.")]
    [SerializeField, TextArea] private string _comboInstruction = "";
    [SerializeField, TextArea] private string _parryInstruction = "공격이 닿기 직전에 막기";
    [SerializeField, TextArea] private string _parryTimingInstruction = "↓ 가드 / 패링";
    [SerializeField, TextArea] private string _retryInstruction = "닿기 직전에 ↓";
    [SerializeField, TextArea] private string _finisherInstruction = "가까이 가서 X로 마무리 공격";

    private PlayerInputHandler _input;
    private EnemyMeleeCombat _combat;
    private bool _initialized;
    private bool _enemyReady;
    private bool _enemyBodyWasSimulated;
    private bool _normalInputWasEnabled;
    private ProCamera2DCinematics _cinematics;
    private Transform _monsterFocus;
    private float _normalCameraSize;
    private bool _hasCameraSize;
    private int _comboStep;
    private bool _parried;
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
        GameEvents.OnParry += HandleParry;
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
                candidate.GetComponent<EnemyMeleeCombat>() == null)
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
            if (candidate == null || candidate.GetComponent<EnemyMeleeCombat>() == null) continue;
            if (match != null) return;
            match = candidate;
        }
        _enemy = match;
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
        Phase = "FirstMonsterFocus";
        _panel.SetActive(false);
        yield return ShowMonsterCinematic();

        Phase = "Encounter";
        Show(_encounterText, "", "");
        yield return WaitRealtime(_noticeDuration);
        _panel.SetActive(false);
        SetPlayerControl(true);

        Phase = "Combo";
        _comboStep = 0;
        SetPlayerControl(true);
        ShowComboProgress();
        while (_comboStep < 3) yield return null;
        SetPlayerControl(false);
        while (_player.StateMachine.CurrentState == _player.AttackState) yield return null;

        _player.StateMachine.ChangeState(_player.IdleState);
        while (!_parried)
        {
            yield return WaitForNeutralPlayer();
            FaceEachOther();
            _enemy.StateMachine.ChangeState(new WaitingState(_enemy));
            _combat.StopAttack();
            SetPlayerControl(true);
            Phase = "Windup";
            Show(_parryText, _parryInstruction, "↓");
            _enemy.Animation.PlayAttack();
            // The guide stays beside the player; movement, guard and game time remain active.
            yield return new WaitForSeconds(Mathf.Max(0.05f, _enemy.Data.AttackPrepareTime));
            Phase = "ParryAttempt";
            Show(_parryTimingText, _parryTimingInstruction, "↓");
            // Use the original attack/guard/parry path. Zero damage keeps practice safe.
            _combat.IncreaseDamage(-100f);
            _combat.Attack();
            float attackTime = 0f;
            while (!_parried && attackTime < _enemy.Data.AttackDurationTime)
            {
                // The original combat enters GroggyState before broadcasting the parry event.
                // Also observe that state so a successful native parry cannot become a retry.
                if (_enemy.IsGroggy)
                {
                    _parried = true;
                    _enemy.TryHoldGroggy();
                    break;
                }

                attackTime += Time.deltaTime;
                yield return null;
            }
            _combat.StopAttack();
            if (!_parried)
            {
                Show(_retryText, _retryInstruction, "↓");
                yield return new WaitForSeconds(_dialogueDuration);
            }
        }

        _enemy.TryHoldGroggy();

        Phase = "Finisher";
        SetPlayerControl(true);
        Show(_finisherText, _finisherInstruction, "X");
        // Keep movement and attack input active. The player's normal states find a target
        // on each attack press, so an out-of-range attempt never locks the tutorial.
        while (_enemy != null && !_enemy.IsDefeated) yield return null;
        _complete = true;
        Show(_completeText, "", "");
        yield return new WaitForSeconds(0.8f);
        ReleaseControl();
        _running = false;
        Phase = "Complete";
    }

    private IEnumerator WaitForNeutralPlayer()
    {
        SetPlayerControl(true);
        while (!_player.IsGrounded || _player.StateMachine.CurrentState == _player.AttackState ||
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
        // The original ClearInputState does not clear the guard buffer.
        _input.TryUseGuardInput();
    }

    private void ConfigurePlayerHint()
    {
        Canvas canvas = _panel.GetComponentInParent<Canvas>();
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
        var parent = panelRect.parent as RectTransform;
        if (parent == null) return;
        Camera view = _camera.GetComponent<Camera>();
        Collider2D body = _player.GetComponent<Collider2D>();
        Vector3 worldPosition = body != null ? body.bounds.center : _player.transform.position;
        Vector3 screenPosition = view.WorldToScreenPoint(worldPosition);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, null, out Vector2 localPosition))
        {
            Vector2 position = localPosition + _hintOffset;
            Vector2 halfSize = panelRect.sizeDelta * 0.5f;
            position.x = Mathf.Clamp(position.x, parent.rect.xMin + halfSize.x, parent.rect.xMax - halfSize.x);
            position.y = Mathf.Clamp(position.y, parent.rect.yMin + halfSize.y, parent.rect.yMax - halfSize.y);
            panelRect.anchoredPosition = position;
        }
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
        yield return new WaitForEndOfFrame();
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
        if (Phase != "Combo" || hitData.ComboStep < 1 || hitData.ComboStep > 3) return;
        _comboStep++;
        ShowComboProgress();
    }

    private void ShowComboProgress()
    {
        string instruction = (_comboInstruction ?? "").Replace("{count}", _comboStep.ToString())
            .Replace("{total}", "3");
        Show(_comboText, instruction, "X");
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
        UpdateHintPosition();
        _dialogue.text = string.IsNullOrEmpty(dialogue) ? instruction :
            string.IsNullOrEmpty(instruction) ? dialogue : dialogue + "\n" + instruction;
        _instruction.text = "";
        _keyPrompt.SetActive(!string.IsNullOrEmpty(key));
        Sprite sprite = key == "X" ? _attackKeySprite : key == "↓" ? _parryKeySprite : null;
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
        if (_running && _cinematics != null && _cinematics.IsPlaying) _cinematics.Stop();
        if (_combat != null)
        {
            _combat.StopAttack();
            _combat.NormalDamage();
        }
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
