using UnityEngine;

public class PlayerInputHandler : MonoBehaviour
{
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

    [Tooltip("아래 방향키를 누른 뒤 이 시간 안에 가드가 시작돼야 패링 판정이 열림")]
    [SerializeField]
    private InputBuffer _guardBuffer = new InputBuffer(0.1f);

    public bool TryUseJumpInput() => _jumpBuffer.TryUse();
    public bool TryUseDodgeInput() => _dodgeBuffer.TryUse();
    public bool TryUseAttackInput() => _attackBuffer.TryUse();
    public bool TryUseRangedAttackInput() => _rangedAttackBuffer.TryUse();
    public bool TryUseGuardInput() => _guardBuffer.TryUse();

    private void Update()
    {
        GetKeyInput();
    }

    private void GetKeyInput()
    {
        _moveInputX = GetHorizontalInput();

        _isJumpKeyDown = Input.GetKeyDown(KeyCode.C);
        _isJumpKey = Input.GetKey(KeyCode.C);
        _isDodgeKeyDown = Input.GetKeyDown(KeyCode.Z);
        _isAttackKeyDown = Input.GetKeyDown(KeyCode.X);
        _isRangedAttackKeyDown = Input.GetKeyDown(KeyCode.V);
        _isRangedAttackKeyUp = Input.GetKeyUp(KeyCode.V);
        _isPotionKeyDown = Input.GetKeyDown(KeyCode.B);
        _isGuardKeyDown = Input.GetKeyDown(KeyCode.DownArrow);
        _isGuardKey = Input.GetKey(KeyCode.DownArrow);


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

        if (_isGuardKeyDown)
        {
            _guardBuffer.Press();
        }
    }

    // 좌우 이동은 방향키만 사용 (Input Manager의 Horizontal에 포함된 A/D 제외)
    private static float GetHorizontalInput()
    {
        float x = 0f;

        if (Input.GetKey(KeyCode.LeftArrow))
        {
            x -= 1f;
        }

        if (Input.GetKey(KeyCode.RightArrow))
        {
            x += 1f;
        }

        return x;
    }
}
