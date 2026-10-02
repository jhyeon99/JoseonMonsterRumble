using UnityEngine;

public class GroundDirectionLock : MonoBehaviour, IAttackDirectionLock
{
    [SerializeField] private EnemyFlip _flip;

    public void LockDirection()
    {
        _flip.enabled = false;
    }

    public void UnlockDirection()
    {
        _flip.enabled = true;
    }
}