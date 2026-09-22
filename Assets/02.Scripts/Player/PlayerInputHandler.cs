using UnityEngine;

public class PlayerInputHandler : MonoBehaviour
{
    private const string Horizontal = "Horizontal";

    private float _moveInputX;
    public float MoveInputX => _moveInputX;
    private bool _isJumpKeyDown;
    public bool IsJumpKeyDown => _isJumpKeyDown;
    private bool _isDodgeKeyDown;
    public bool IsDodgeKeyDown => _isDodgeKeyDown;

    private void Update()
    {
        GetKeyInput();
    }

    private void GetKeyInput()
    {
        _moveInputX = Input.GetAxisRaw(Horizontal);
        _isJumpKeyDown = Input.GetKeyDown(KeyCode.Space);
        _isDodgeKeyDown = Input.GetKey(KeyCode.Z);
    }
}