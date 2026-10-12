using System.Collections.Generic;
using UnityEngine;

namespace GameAudio
{
    [DisallowMultipleComponent]
    public sealed class EventAudio : ActorAudio
    {
        private string _category;
        private ulong _execution;
        private readonly List<PlayHandle> _handles = new List<PlayHandle>();

        public static EventAudio For(Component source, string category)
        {
            if (!source.TryGetComponent(out EventAudio emitter))
            {
                emitter = source.gameObject.AddComponent<EventAudio>();
            }
            emitter._category = category;
            return emitter;
        }

        protected override SoundSetSO ResolveSet() => Manager?.Catalog?.GetSet(_category);

        public void Begin()
        {
            Cancel();
            _execution = BeginAction();
        }

        public PlayHandle Play(string eventId)
        {
            if (_execution == 0) _execution = BeginAction();
            PlayHandle handle = PlayAction(eventId, _execution);
            if (!handle.IsEmpty) _handles.Add(handle);
            return handle;
        }

        public void Complete()
        {
            EndAction(_execution, true);
            _execution = 0;
        }

        public void Cancel()
        {
            EndAction(_execution, false);
            _execution = 0;
            if (Manager != null)
            {
                foreach (PlayHandle handle in _handles) Manager.StopHandle(handle);
            }
            _handles.Clear();
        }

        protected override void OnDisable()
        {
            Cancel();
            base.OnDisable();
        }
    }
}
