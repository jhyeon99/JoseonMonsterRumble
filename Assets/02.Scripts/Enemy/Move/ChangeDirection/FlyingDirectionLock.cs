using UnityEngine;

public class FlyingDirectionLock : MonoBehaviour, IAttackDirectionLock
{
    [SerializeField] private EnemyFlip _flip;
    [SerializeField] private EnemyRotation _rotation;

    public void LockDirection()
    {
        _flip.enabled = false;
        _rotation.enabled = false;
    }

    public void UnlockDirection()
    {
        _flip.enabled = true;
        _rotation.enabled = true;
    }
}