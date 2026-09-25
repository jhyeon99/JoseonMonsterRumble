using UnityEngine;

public class PlatformCollisionForwarder : MonoBehaviour
{
    private MovingPlatform _parentPlatform;

    private void Awake()
    {
        // 부모 오브젝트의 MovingPlatform 스크립트 참조
        _parentPlatform = GetComponentInParent<MovingPlatform>();
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