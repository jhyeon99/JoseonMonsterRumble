using System.Collections.Generic;
using UnityEngine;

namespace GameAudio
{
    [DefaultExecutionOrder(100)]
    public abstract class ActorAudio : MonoBehaviour
    {
        [SerializeField] protected SoundSetSO _soundSet;
        protected AudioManager Manager { get; private set; }
        public AudioOwnerKey OwnerKey { get; private set; }
        private ulong _nextExecution;
        private readonly HashSet<ulong> _actions = new HashSet<ulong>();
        private readonly HashSet<string> _warnedPolicies = new HashSet<string>();
        private bool _retired;

        protected virtual void OnEnable()
        {
            Manager = AudioManager.Instance;
            OwnerKey = new AudioOwnerKey(GetInstanceID(), AudioLifetimeRegistry.NextOwnerGeneration());
            _retired = false;
        }

        protected virtual void OnDisable() => RetireOwner();
        protected abstract SoundSetSO ResolveSet();

        protected ulong BeginAction()
        {
            if (_retired || !isActiveAndEnabled) return 0;
            if (++_nextExecution == 0) ++_nextExecution;
            _actions.Add(_nextExecution);
            return _nextExecution;
        }

        protected void EndAction(ulong execution, bool completed)
        {
            if (execution == 0 || !_actions.Remove(execution) || Manager == null) return;
            if (completed) Manager.CompleteAction(OwnerKey, execution);
            else Manager.CancelAction(OwnerKey, execution);
        }

        protected void CancelAllActions()
        {
            if (Manager != null) foreach (ulong execution in _actions) Manager.CancelAction(OwnerKey, execution);
            _actions.Clear();
        }

        protected void RetireOwner()
        {
            CancelAllActions();
            if (Manager != null && !OwnerKey.IsEmpty) Manager.ReleaseOwner(OwnerKey);
            _retired = true;
        }

        public AudioContext CreateActionContext(ulong execution) =>
            new AudioContext(OwnerKey, execution, AudioLifetimeRegistry.ForScene(gameObject.scene), transform.position);

        public AudioContext CreateResultContext(Vector3 position) =>
            new AudioContext(OwnerKey, 0, AudioLifetimeRegistry.ForScene(gameObject.scene), position);

        protected PlayHandle PlayAction(string eventId, ulong execution, bool dedicatedLoop = false)
        {
            if (_retired || execution == 0) return PlayHandle.Empty;
            return Request(eventId, LifetimePolicy.Action, CreateActionContext(execution), dedicatedLoop);
        }

        protected PlayHandle PlayResult(string eventId, Vector3 position) =>
            Request(eventId, LifetimePolicy.Result, CreateResultContext(position));

        protected PlayHandle PlayEntity(string eventId) => _retired ? PlayHandle.Empty :
            Request(eventId, LifetimePolicy.Entity, CreateResultContext(transform.position));

        private PlayHandle Request(string eventId, LifetimePolicy expected, AudioContext context, bool dedicatedLoop = false)
        {
            SoundSetSO set = ResolveSet();
            if (Manager == null || set == null || !set.TryGetEvent(eventId, out EventBinding binding) || !binding.Enabled)
                return PlayHandle.Empty;
            if (binding.LifetimePolicy != expected)
            {
                if (_warnedPolicies.Add(eventId)) Debug.LogWarning($"{name}: {eventId}의 수명 정책은 {expected}로 설정하세요.", this);
                return PlayHandle.Empty;
            }
            return dedicatedLoop ? Manager.PlayDedicatedActionLoop(set, eventId, context) : Manager.PlayEvent(set, eventId, context);
        }
    }
}
