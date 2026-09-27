using UnityEngine;

public class PlatformCollisionForwarder : MonoBehaviour
{
    private MovingPlatform _parentPlatform;

    private void Awake()
    {
        _parentPlatform = GetComponentInParent<MovingPlatform>();

        if (_parentPlatform == null)
        {
            Debug.LogWarning($"{name}: Moving Platform 참조가 없습니다.", this);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (_parentPlatform != null)
        {
            _parentPlatform.OnPlatformCollisionEnter2D(collision);
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (_parentPlatform != null)
        {
            _parentPlatform.OnPlatformCollisionExit2D(collision);
        }
    }
}
