using UnityEngine;

[System.Serializable]
public class ActionMotionData
{
    [SerializeField] private AnimationClip _clip;
    [SerializeField, Min(0.01f)] private float _animatorSpeed = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float _cancelRatio = 0.8f;
    [SerializeField, Min(0.01f)] private float _fallbackDuration = 0.6f;

    public AnimationClip Clip => _clip;
    public float AnimatorSpeed => _animatorSpeed;
    public float CancelRatio => _cancelRatio;

    public float TotalDuration => (_clip != null && _animatorSpeed > 0f)
        ? _clip.length / _animatorSpeed
        : _fallbackDuration;

    public float CancelStartTime => TotalDuration * _cancelRatio;

    public bool CanCancel(float elapsedTime) => elapsedTime >= CancelStartTime;

    public void BindClipIfEmpty(AnimationClip clip, float defaultSpeed = 1f)
    {
        if (_clip == null)
        {
            _clip = clip;
        }

        if (_animatorSpeed <= 0f)
        {
            _animatorSpeed = defaultSpeed;
        }
    }
}
