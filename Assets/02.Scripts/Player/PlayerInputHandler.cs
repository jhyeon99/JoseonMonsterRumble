using UnityEngine;

using System.Collections.Generic;

[DefaultExecutionOrder(-100)]
public class PlayerInputHandler : MonoBehaviour
{
    private static readonly KeyCode[] GameplayKeys =
    {
        KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.DownArrow,
        KeyCode.C, KeyCode.Space, KeyCode.Z, KeyCode.X, KeyCode.V, KeyCode.Alpha1, KeyCode.Keypad1
    };
    private readonly HashSet<KeyCode> _suppressedKeys = new HashSet<KeyCode>(GameplayKeys.Length);
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

    public bool TryUseJumpInput() => _jumpBuffer.TryUse();
    public bool TryUseDodgeInput() => _dodgeBuffer.TryUse();
    public bool TryUseAttackInput() => _attackBuffer.TryUse();
    public void ResetAttackInput() => _attackBuffer.Clear();
    public bool TryUseRangedAttackInput() => _rangedAttackBuffer.TryUse();
    public bool TryUsePotionInput() => _potionBuffer.TryUse();
    public bool TryUseGuardInput() => _guardBuffer.TryUse();

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
        return !_suppressedKeys.Contains(key) && Input.GetKey(key);
    }

    private bool IsKeyPressed(KeyCode key)
    {
        return !_suppressedKeys.Contains(key) && Input.GetKeyDown(key);
    }

    private void GetKeyInput()
    {
        _moveInputX = GetHorizontalInput();

        _isJumpKeyDown = IsKeyPressed(KeyCode.C) || IsKeyPressed(KeyCode.Space);
        _isJumpKey = IsKeyHeld(KeyCode.C) || IsKeyHeld(KeyCode.Space);
        _isDodgeKeyDown = IsKeyPressed(KeyCode.Z);
        _isAttackKeyDown = IsKeyPressed(KeyCode.X);
        _isRangedAttackKeyDown = IsKeyPressed(KeyCode.V);
        // Preserve a release that happened while paused without recording menu presses.
        bool isRangedAttackHeld = Input.GetKey(KeyCode.V);
        _isRangedAttackKeyUp = Input.GetKeyUp(KeyCode.V) || (_wasRangedAttackHeld && !isRangedAttackHeld);
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

