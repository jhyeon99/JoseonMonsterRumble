using UnityEngine;

public class PlayerInputHandler : MonoBehaviour
{
    private const string Horizontal = "Horizontal";

    private float _moveInputX;
    public float MoveInputX => _moveInputX;
    private bool _isJumpKeyDown;
    private bool _isDodgeKeyDown;
    private bool _isAttackKeyDown;

    [Header("Input Buffer")]
    [SerializeField] private InputBuffer _jumpBuffer = new InputBuffer(0.1f);
    [SerializeField] private InputBuffer _dodgeBuffer = new InputBuffer(0.1f);
    [SerializeField] private InputBuffer _attackBuffer = new InputBuffer(0.1f);

    public bool TryUseJumpInput() => _jumpBuffer.TryUse();
    public bool TryUseDodgeInput() => _dodgeBuffer.TryUse();
    public bool TryUseAttackInput() => _attackBuffer.TryUse();

    private void Update()
    {
        GetKeyInput();
    }

    private void GetKeyInput()
    {
        _moveInputX = Input.GetAxis(Horizontal);
        _isJumpKeyDown = Input.GetKeyDown(KeyCode.Space);
        _isDodgeKeyDown = Input.GetKeyDown(KeyCode.Z);
        _isAttackKeyDown = Input.GetKeyDown(KeyCode.X);

        if (_isJumpKeyDown) _jumpBuffer.Press();
        if (_isDodgeKeyDown) _dodgeBuffer.Press();
        if (_isAttackKeyDown) _attackBuffer.Press();
    }
}