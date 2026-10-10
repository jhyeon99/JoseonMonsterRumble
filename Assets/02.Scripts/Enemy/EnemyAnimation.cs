using UnityEngine;

public enum EnemyHitAnimationMode
{
    AllDamage,
    ThirdComboOrParry,
    ThirdComboOrReflectedProjectile,
}

public enum EnemyWalkPhase { None, Start, Loop, End }

public class EnemyAnimation : MonoBehaviour
{
    private const int ThirdComboStep = 3;
    private const int AnimationLayer = 0;
    private static readonly int HitTagHash = Animator.StringToHash("Hit");
    private static readonly int AttackTagHash = Animator.StringToHash("Attack");
    private static readonly int FlightSpeedHash = Animator.StringToHash("flightSpeed");
    private static readonly int DieTagHash = Animator.StringToHash("Die");
    private static readonly int WalkStartTagHash = Animator.StringToHash("WalkStart");
    private static readonly int WalkLoopTagHash = Animator.StringToHash("WalkLoop");
    private static readonly int WalkEndTagHash = Animator.StringToHash("WalkEnd");

    [SerializeField] private Animator _animator;
    [SerializeField] private EnemyHitAnimationMode _hitAnimationMode = EnemyHitAnimationMode.AllDamage;

    private readonly int _isRunning = Animator.StringToHash("isRunning");
    private readonly int _attack = Animator.StringToHash("attack");
    private readonly int _provoke = Animator.StringToHash("provoke");
    private readonly int _hit = Animator.StringToHash("hit");
    private readonly int _die = Animator.StringToHash("die");
    private readonly int _isGroggy = Animator.StringToHash("isGroggy");

    // 그로기 모션이 애니메이터에 추가되기 전에도 경고 없이 동작하도록 파라미터 유무를 한 번만 확인
    private bool? _hasGroggyParam;
    private bool? _hasFlightSpeedParam;

    public void SetFlightAnimationSpeed(float speed)
    {
        if (_animator == null)
        {
            return;
        }
        if (!_hasFlightSpeedParam.HasValue)
        {
            _hasFlightSpeedParam = HasParameter(FlightSpeedHash);
            if (!_hasFlightSpeedParam.Value)
            {
                Debug.LogError("EnemyAnimation: 비행 재생 속도 파라미터 flightSpeed가 없습니다.", this);
            }
        }
        if (_hasFlightSpeedParam.Value)
        {
            _animator.SetFloat(FlightSpeedHash, speed);
        }
    }

    public void SetRunning(bool isRunning)
    {
        _animator.SetBool(_isRunning, isRunning);
    }

    public EnemyWalkPhase GetWalkPhase(out float progress)
    {
        AnimatorStateInfo state = _animator.IsInTransition(AnimationLayer)
            ? _animator.GetNextAnimatorStateInfo(AnimationLayer)
            : _animator.GetCurrentAnimatorStateInfo(AnimationLayer);
        progress = Mathf.Clamp01(state.normalizedTime);
        if (state.tagHash == WalkStartTagHash)
        {
            return EnemyWalkPhase.Start;
        }
        if (state.tagHash == WalkLoopTagHash)
        {
            return EnemyWalkPhase.Loop;
        }
        return state.tagHash == WalkEndTagHash ? EnemyWalkPhase.End : EnemyWalkPhase.None;
    }

    public void PlayAttack() => _animator.SetTrigger(_attack);

    public bool TryGetAttackPlayback(out AnimatorStateInfo state, out float playbackSpeed)
    {
        state = default;
        playbackSpeed = 0f;
        if (_animator == null || _animator.IsInTransition(AnimationLayer))
        {
            return false;
        }

        state = _animator.GetCurrentAnimatorStateInfo(AnimationLayer);
        playbackSpeed = _animator.speed * state.speed * state.speedMultiplier;
        return state.tagHash == AttackTagHash;
    }

    public void ReplayAttackFrom(int stateHash, float normalizedTime)
    {
        _animator.Play(stateHash, AnimationLayer, normalizedTime);
    }

    public void ResetAttackTrigger() => _animator.ResetTrigger(_attack);
    public void PlayProvoke() => _animator.SetTrigger(_provoke);
    public void ResetProvokeTrigger() => _animator.ResetTrigger(_provoke);
    public void PlayHit() => _animator.SetTrigger(_hit);

    public void PlayDamageHit(HitData hitData)
    {
        if (_animator == null)
        {
            return;
        }

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

    public bool IsPlayingHit() => IsPlayingTaggedState(HitTagHash);

    public bool IsPlayingAttack() => IsPlayingTaggedState(AttackTagHash);

    private bool IsPlayingTaggedState(int tagHash)
    {
        if (_animator == null)
        {
            return false;
        }

        return _animator.GetCurrentAnimatorStateInfo(AnimationLayer).tagHash == tagHash ||
            (_animator.IsInTransition(AnimationLayer) &&
             _animator.GetNextAnimatorStateInfo(AnimationLayer).tagHash == tagHash);
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

    public void PlayDie()
    {
        _animator.ResetTrigger(_attack);
        _animator.ResetTrigger(_hit);
        _animator.SetTrigger(_die);
    }

    public bool IsDeathAnimationComplete()
    {
        if (_animator == null || _animator.IsInTransition(AnimationLayer))
        {
            return false;
        }
        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(AnimationLayer);
        return state.tagHash == DieTagHash && state.normalizedTime >= 1f;
    }

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
