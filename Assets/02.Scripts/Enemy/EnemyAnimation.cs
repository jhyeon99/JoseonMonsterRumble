using UnityEngine;

public enum EnemyHitAnimationMode
{
    AllDamage,
    ThirdComboOrParry,
    ThirdComboOrReflectedProjectile,
}

public class EnemyAnimation : MonoBehaviour
{
    private const int ThirdComboStep = 3;
    private const int AnimationLayer = 0;
    private static readonly int HitTagHash = Animator.StringToHash("Hit");

    [SerializeField] private Animator _animator;
    [SerializeField] private EnemyHitAnimationMode _hitAnimationMode = EnemyHitAnimationMode.AllDamage;

    private readonly int _isRunning = Animator.StringToHash("isRunning");
    private readonly int _attack = Animator.StringToHash("attack");
    private readonly int _hit = Animator.StringToHash("hit");
    private readonly int _die = Animator.StringToHash("die");
    private readonly int _isGroggy = Animator.StringToHash("isGroggy");

    // 그로기 모션이 애니메이터에 추가되기 전에도 경고 없이 동작하도록 파라미터 유무를 한 번만 확인
    private bool? _hasGroggyParam;

    public void SetRunning(bool isRunning)
    {
        _animator.SetBool(_isRunning, isRunning);
    }

    public void PlayAttack() => _animator.SetTrigger(_attack);
    public void PlayHit() => _animator.SetTrigger(_hit);

    public void PlayDamageHit(HitData hitData)
    {
        if (hitData.IsTalisman)
        {
            if (!IsPlayingHit())
            {
                _animator.ResetTrigger(_attack);
                PlayHit();
            }

            return;
        }

        if (ShouldPlayDamageHit(hitData))
        {
            PlayHit();
        }
    }

    public bool IsPlayingHit()
    {
        return _animator.GetCurrentAnimatorStateInfo(AnimationLayer).tagHash == HitTagHash ||
            (_animator.IsInTransition(AnimationLayer) &&
             _animator.GetNextAnimatorStateInfo(AnimationLayer).tagHash == HitTagHash);
    }

    public void PlayParriedHit()
    {
        if (_hitAnimationMode == EnemyHitAnimationMode.ThirdComboOrParry)
        {
            PlayHit();
        }
    }

    public void PlayLaunchHit()
    {
        if (_hitAnimationMode == EnemyHitAnimationMode.AllDamage)
        {
            PlayHit();
        }
    }

    public void PlayDie() => _animator.SetTrigger(_die);

    public void SetGroggy(bool isGroggy)
    {
        if (_animator == null)
        {
            return;
        }

        _hasGroggyParam ??= HasParameter(_isGroggy);
        if (_hasGroggyParam.Value)
        {
            _animator.SetBool(_isGroggy, isGroggy);
        }
    }

    private bool ShouldPlayDamageHit(HitData hitData)
    {
        return _hitAnimationMode switch
        {
            EnemyHitAnimationMode.AllDamage => true,
            EnemyHitAnimationMode.ThirdComboOrParry => hitData.ComboStep == ThirdComboStep,
            EnemyHitAnimationMode.ThirdComboOrReflectedProjectile =>
                hitData.ComboStep == ThirdComboStep || hitData.IsReflectedProjectile,
            _ => false,
        };
    }

    private bool HasParameter(int nameHash)
    {
        foreach (AnimatorControllerParameter parameter in _animator.parameters)
        {
            if (parameter.nameHash == nameHash)
            {
                return true;
            }
        }

        return false;
    }
}
