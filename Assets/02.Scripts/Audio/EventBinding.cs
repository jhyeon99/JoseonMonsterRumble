using System;
using UnityEngine;

namespace GameAudio
{
    [Serializable]
    public sealed class EventBinding
    {
        public string EventId;
        public bool Enabled = true;
        public string SoundId;
        [TextArea] public string TriggerDescription;
        [Min(0f)] public float Delay;
        public AudioTimeMode TimeMode = AudioTimeMode.GameTime;
        public LifetimePolicy LifetimePolicy = LifetimePolicy.Action;
        public bool Loop;
        [Min(0f)] public float MinInterval;
        public IntervalScope IntervalScope = IntervalScope.Entity;
        [Tooltip("BGM 전용. 음수이면 매니저의 기본 페이드 시간을 사용합니다.")]
        public float BgmFadeDuration = -1f;
        [Tooltip("반복음의 종료 이벤트/조건. 커스텀 인스펙터 검사에 사용합니다.")]
        public string LoopEndRule;

        public float EffectiveDelay => Mathf.Max(0f, Delay);
        public float EffectiveMinInterval => Mathf.Max(0f, MinInterval);
        public bool HasValidLifetime => !Loop ||
            (LifetimePolicy != GameAudio.LifetimePolicy.Result && !string.IsNullOrWhiteSpace(LoopEndRule));
    }
}
