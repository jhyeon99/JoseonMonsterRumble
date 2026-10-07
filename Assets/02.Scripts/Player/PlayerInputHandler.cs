using UnityEngine;

public class PlayerInputHandler : MonoBehaviour
{
    private const string Horizontal = "Horizontal";

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


    [Header("Input Buffer")]
    [SerializeField] private InputBuffer _jumpBuffer = new InputBuffer(0.1f);

    [SerializeField] private InputBuffer _dodgeBuffer = new InputBuffer(0.1f);
    [SerializeField] private InputBuffer _attackBuffer = new InputBuffer(0.1f);
    [SerializeField] private InputBuffer _rangedAttackBuffer = new InputBuffer(0.1f);

    public bool TryUseJumpInput() => _jumpBuffer.TryUse();
    public bool TryUseDodgeInput() => _dodgeBuffer.TryUse();
    public bool TryUseAttackInput() => _attackBuffer.TryUse();
    public bool TryUseRangedAttackInput() => _rangedAttackBuffer.TryUse();

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
        _jumpBuffer.Clear();
        _dodgeBuffer.Clear();
        _attackBuffer.Clear();
        _rangedAttackBuffer.Clear();
    }
    private void Update()
    {
        GetKeyInput();
    }

    private void GetKeyInput()
    {
        _moveInputX = Input.GetAxis(Horizontal);

        _isJumpKeyDown = Input.GetKeyDown(KeyCode.C);
        _isJumpKey = Input.GetKey(KeyCode.C);
        _isDodgeKeyDown = Input.GetKeyDown(KeyCode.Z);
        _isAttackKeyDown = Input.GetKeyDown(KeyCode.X);
        _isRangedAttackKeyDown = Input.GetKeyDown(KeyCode.V);
        _isRangedAttackKeyUp = Input.GetKeyUp(KeyCode.V);
        _isPotionKeyDown = Input.GetKeyDown(KeyCode.B);


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
    }
}

