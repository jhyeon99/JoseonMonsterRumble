using UnityEngine;

public class EnemyAnimation : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    private readonly int _isRunning = Animator.StringToHash("isRunning");
    private readonly int _attack = Animator.StringToHash("attack");
    private readonly int _hit = Animator.StringToHash("hit");
    private readonly int _die = Animator.StringToHash("die");

    public void SetRunning(bool isRunning)
    {
        _animator.SetBool(_isRunning, isRunning);
    }

    public void PlayAttack() => _animator.SetTrigger(_attack);
    public void PlayHit() => _animator.SetTrigger(_hit);
    public void PlayDie() => _animator.SetTrigger(_die);
}