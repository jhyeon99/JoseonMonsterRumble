using UnityEngine;

using System.Collections.Generic;

[System.Flags]
public enum GameplayInputActions
{
    None = 0,
    Movement = 1,
    Jump = 2,
    Dodge = 4,
    Attack = 8,
    RangedAttack = 16,
    Potion = 32,
    Guard = 64,
    All = Movement | Jump | Dodge | Attack | RangedAttack | Potion | Guard
}

[DefaultExecutionOrder(-100)]
public class PlayerInputHandler : MonoBehaviour
{
    private static readonly KeyCode[] GameplayKeys =
    {
        KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.DownArrow,
        KeyCode.C, KeyCode.Space, KeyCode.Z, KeyCode.X, KeyCode.V, KeyCode.Alpha1, KeyCode.Keypad1
    };
    private readonly HashSet<KeyCode> _suppressedKeys = new HashSet<KeyCode>(GameplayKeys.Length);
    private Object _tutorialInputOwner;
    private GameplayInputActions _tutorialActions = GameplayInputActions.All;
    private float _nextTutorialAttackAt;
    private bool _wasRangedAttackHeld;
    private float _moveInputX;
    public float MoveInputX => _moveInputX;

    private bool _isJumpKeyDown;
    private bool _isJumpKey;
    public bool IsJumpKey => _isJumpKey;
    private bool _isDodgeKeyDown;
    private bool _isAttackKeyDown;
    private bool _isRangedAttackKeyDown;
    private bool _isRangedAttackKeyUp;
    public bool IsRangedAttackKeyUp => _isRangedAttackKeyUp;
    private bool _isPotionKeyDown;
    public bool IsPotionKeyDown => _isPotionKeyDown;
    private bool _isGuardKeyDown;
    private bool _isGuardKey;
    public bool IsGuardKey => _isGuardKey;


    [Header("Input Buffer")]
    [SerializeField] private InputBuffer _jumpBuffer = new InputBuffer(0.1f);

    [SerializeField] private InputBuffer _dodgeBuffer = new InputBuffer(0.1f);
    [SerializeField] private InputBuffer _attackBuffer = new InputBuffer(0.1f);
    [SerializeField] private InputBuffer _rangedAttackBuffer = new InputBuffer(0.1f);
    [SerializeField] private InputBuffer _potionBuffer = new InputBuffer(0.1f);

    [Tooltip("아래 방향키를 누른 뒤 이 시간 안에 가드가 시작돼야 패링 판정이 열림")]
    [SerializeField]
    private InputBuffer _guardBuffer = new InputBuffer(0.1f);

    public bool TryUseJumpInput() => IsActionAllowed(GameplayInputActions.Jump) && _jumpBuffer.TryUse();
    public bool TryUseDodgeInput() => IsActionAllowed(GameplayInputActions.Dodge) && _dodgeBuffer.TryUse();
    public bool TryUseAttackInput() => IsActionAllowed(GameplayInputActions.Attack) && _attackBuffer.TryUse();
    public void ResetAttackInput() => _attackBuffer.Clear();
    public bool TryUseRangedAttackInput() => IsActionAllowed(GameplayInputActions.RangedAttack) && _rangedAttackBuffer.TryUse();
    public bool TryUsePotionInput() => IsActionAllowed(GameplayInputActions.Potion) && _potionBuffer.TryUse();
    public bool TryUseGuardInput() => IsActionAllowed(GameplayInputActions.Guard) && _guardBuffer.TryUse();

    public void SetTutorialInputRestriction(Object owner, GameplayInputActions actions)
    {
        if (owner == null || (_tutorialInputOwner == owner && _tutorialActions == actions)) return;
        bool keepGuardHeld = _isGuardKey && (actions & GameplayInputActions.Guard) != 0;
        _tutorialInputOwner = owner;
        _tutorialActions = actions;
        _nextTutorialAttackAt = 0f;
        ClearInputState();
        _isGuardKey = keepGuardHeld;
        if ((actions & GameplayInputActions.RangedAttack) == 0 && TryGetComponent(out PlayerRangedAttack rangedAttack))
            rangedAttack.CancelCharge();
    }

    public void ClearTutorialInputRestriction(Object owner)
    {
        if (_tutorialInputOwner != owner) return;
        _tutorialInputOwner = null;
        _tutorialActions = GameplayInputActions.All;
        ClearInputState();
    }

    private bool IsActionAllowed(GameplayInputActions action) =>
        _tutorialInputOwner == null || (_tutorialActions & action) != 0;

    private static GameplayInputActions GetKeyAction(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.LeftArrow:
            case KeyCode.RightArrow: return GameplayInputActions.Movement;
            case KeyCode.C:
            case KeyCode.Space: return GameplayInputActions.Jump;
            case KeyCode.Z: return GameplayInputActions.Dodge;
            case KeyCode.X: return GameplayInputActions.Attack;
            case KeyCode.V: return GameplayInputActions.RangedAttack;
            case KeyCode.DownArrow: return GameplayInputActions.Guard;
            default: return GameplayInputActions.Potion;
        }
    }

    public void ClearInputState()
    {
        _moveInputX = 0f;
        _isJumpKeyDown = false;
        _isJumpKey = false;
        _isDodgeKeyDown = false;
        _isAttackKeyDown = false;
        _isRangedAttackKeyDown = false;
        _isRangedAttackKeyUp = false;
        _isPotionKeyDown = false;
        _isGuardKeyDown = false;
        _isGuardKey = false;
        _guardBuffer.Clear();
        _jumpBuffer.Clear();
        _dodgeBuffer.Clear();
        _attackBuffer.Clear();
        _rangedAttackBuffer.Clear();
        _potionBuffer.Clear();
    }
    private void Update()
    {
        if (PauseController.IsGameplayInputBlocked)
        {
            ClearInputState();
            foreach (KeyCode key in GameplayKeys)
            {
                if (GetKeyAction(key) == GameplayInputActions.Movement)
                {
                    continue;
                }

                if (Input.GetKey(key))
                {
                    _suppressedKeys.Add(key);
                }
                else
                {
                    _suppressedKeys.Remove(key);
                }
            }
            return;
        }

        foreach (KeyCode key in GameplayKeys)
        {
            if (!Input.GetKey(key))
            {
                _suppressedKeys.Remove(key);
            }
        }
        GetKeyInput();
    }

    private bool IsKeyHeld(KeyCode key)
    {
        GameplayInputActions action = GetKeyAction(key);
        // A guided hold can continue across its explanation screen. Normal menu inputs stay suppressed.
        // Movement can also continue or be pre-held across tutorial/pause screens for smooth UX.
        bool guidedHold = action == GameplayInputActions.Movement ||
            (_tutorialInputOwner != null &&
            (action == GameplayInputActions.Guard || action == GameplayInputActions.Attack));
        return IsActionAllowed(action) && (guidedHold || !_suppressedKeys.Contains(key)) && Input.GetKey(key);
    }

    private bool IsKeyPressed(KeyCode key)
    {
        return IsActionAllowed(GetKeyAction(key)) && !_suppressedKeys.Contains(key) && Input.GetKeyDown(key);
    }

    private void GetKeyInput()
    {
        _moveInputX = GetHorizontalInput();

        _isJumpKeyDown = IsKeyPressed(KeyCode.C) || IsKeyPressed(KeyCode.Space);
        _isJumpKey = IsKeyHeld(KeyCode.C) || IsKeyHeld(KeyCode.Space);
        _isDodgeKeyDown = IsKeyPressed(KeyCode.Z);
        _isAttackKeyDown = IsKeyPressed(KeyCode.X);
        if (_tutorialInputOwner != null && IsKeyHeld(KeyCode.X) && Time.unscaledTime >= _nextTutorialAttackAt)
        {
            _isAttackKeyDown = true;
            _nextTutorialAttackAt = Time.unscaledTime + 0.2f;
        }
        _isRangedAttackKeyDown = IsKeyPressed(KeyCode.V);
        // Preserve a release that happened while paused without recording menu presses.
        bool isRangedAttackHeld = Input.GetKey(KeyCode.V);
        _isRangedAttackKeyUp = IsActionAllowed(GameplayInputActions.RangedAttack) &&
            (Input.GetKeyUp(KeyCode.V) || (_wasRangedAttackHeld && !isRangedAttackHeld));
        _wasRangedAttackHeld = isRangedAttackHeld;
        _isPotionKeyDown = IsKeyPressed(KeyCode.Alpha1) || IsKeyPressed(KeyCode.Keypad1);
        _isGuardKeyDown = IsKeyPressed(KeyCode.DownArrow);
        _isGuardKey = IsKeyHeld(KeyCode.DownArrow);


        if (_isJumpKeyDown)
        {
            _jumpBuffer.Press();
        }

        if (_isDodgeKeyDown)
        {
            _dodgeBuffer.Press();
        }

        if (_isAttackKeyDown)
        {
            _attackBuffer.Press();
        }

        if (_isRangedAttackKeyDown)
        {
            _rangedAttackBuffer.Press();
        }

        if (_isPotionKeyDown)
        {
            _potionBuffer.Press();
        }

        if (_isGuardKeyDown)
        {
            _guardBuffer.Press();
        }
    }

    // 좌우 이동은 방향키만 사용 (Input Manager의 Horizontal에 포함된 A/D 제외)
    private float GetHorizontalInput()
    {
        float x = 0f;

        if (IsKeyHeld(KeyCode.LeftArrow))
        {
            x -= 1f;
        }

        if (IsKeyHeld(KeyCode.RightArrow))
        {
            x += 1f;
        }

        return x;
    }
}

