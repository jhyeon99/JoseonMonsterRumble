using UnityEngine;

namespace GameAudio
{
    // Two footfalls per animation cycle; a timer supports objects without an animator.
    public sealed class FootstepClock
    {
        private int _stateHash;
        private int _step = -1;
        private float _timer;

        public bool Tick(bool moving, Animator animator)
        {
            if (!moving || Time.deltaTime <= 0f)
            {
                _step = -1;
                _timer = 0f;
                return false;
            }
            if (animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                int step = Mathf.FloorToInt(state.normalizedTime * 2f);
                bool changed = state.fullPathHash != _stateHash || step != _step;
                _stateHash = state.fullPathHash;
                _step = step;
                return changed;
            }
            _timer -= Time.deltaTime;
            if (_timer > 0f) return false;
            _timer = 0.32f;
            return true;
        }
    }
}
