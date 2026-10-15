using System.Collections;
using System.Collections.Generic;
using GameAudio;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossAudio : ActorAudio
{
    private Boss _boss;
    private BossHealth _health;
    private BossPattern[] _patterns;
    private readonly Dictionary<BossPattern, ulong> _patternExecutions = new Dictionary<BossPattern, ulong>();
    private ulong _stateExecution;
    private readonly Dictionary<BossPattern, PlayHandle> _chargeLoops = new Dictionary<BossPattern, PlayHandle>();
    private bool _defeated;
    private Coroutine _frogAmbience;
    private PlayHandle _frogHandle;

    protected override void OnEnable()
    {
        base.OnEnable();
        _boss = GetComponent<Boss>();
        _health = GetComponent<BossHealth>();
        _defeated = false;
        if (_health != null) _health.OnDamaged += OnDamaged;
        if (_boss != null) _boss.OnAudioStateChanged += OnStateChanged;
        Boss.OnDefeated += OnDefeated;
        Boss.OnFightStarted += OnFightStarted;
        if (_boss != null) _boss.OnPhaseChanged += OnPhaseChanged;
        _patterns = GetComponentsInChildren<BossPattern>(true);
        foreach (BossPattern pattern in _patterns)
        {
            pattern.OnExecutionStarted += OnPatternStarted;
            pattern.OnExecutionEnded += OnPatternEnded;
            pattern.OnAudioCue += OnPatternCue;
        }
    }

    protected override void OnDisable()
    {
        StopFrogAmbience();
        if (_health != null) _health.OnDamaged -= OnDamaged;
        if (_boss != null) _boss.OnAudioStateChanged -= OnStateChanged;
        Boss.OnDefeated -= OnDefeated;
        Boss.OnFightStarted -= OnFightStarted;
        if (_boss != null) _boss.OnPhaseChanged -= OnPhaseChanged;
        if (_patterns != null)
        {
            foreach (BossPattern pattern in _patterns)
            {
                if (pattern == null) continue;
                pattern.OnExecutionStarted -= OnPatternStarted;
                pattern.OnExecutionEnded -= OnPatternEnded;
                pattern.OnAudioCue -= OnPatternCue;
            }
        }
        base.OnDisable();
        _patternExecutions.Clear();
        _chargeLoops.Clear();
        _stateExecution = 0;
    }

    protected override SoundSetSO ResolveSet() => _soundSet != null ? _soundSet : Manager?.Catalog?.Boss;
    private void OnFightStarted(Boss boss)
    {
        if (boss != _boss || _defeated || Manager == null) return;
        Manager.PlayMusicEvent("Music.Boss", gameObject.scene);
        if (_frogAmbience == null) _frogAmbience = StartCoroutine(PlayFrogAmbience());
    }
    private IEnumerator PlayFrogAmbience()
    {
        while (!_defeated && Manager != null)
        {
            AudioCatalogSO catalog = Manager.Catalog;
            float first = catalog != null ? catalog.BossFrogMinInterval : 3f;
            float second = catalog != null ? catalog.BossFrogMaxInterval : 8f;
            float min = Mathf.Max(0.1f, Mathf.Min(first, second));
            float max = Mathf.Max(min, Mathf.Max(first, second));
            yield return new WaitForSeconds(Random.Range(min, max));
            if (_defeated || Manager == null) break;

            // 클립을 끝까지 재생한 다음 쉬므로 울음끼리 겹치지 않는다.
            SoundSetSO set = ResolveSet();
            if (set != null && set.TryGetEvent("Boss.FrogAmbience", out EventBinding binding) && !binding.Loop)
            {
                _frogHandle = PlayEntity("Boss.FrogAmbience");
                while (Manager != null && Manager.IsHandleValid(_frogHandle)) yield return null;
                _frogHandle = PlayHandle.Empty;
            }
        }
        _frogAmbience = null;
    }

    private void StopFrogAmbience()
    {
        if (_frogAmbience != null) StopCoroutine(_frogAmbience);
        _frogAmbience = null;
        if (Manager != null) Manager.StopHandle(_frogHandle);
        _frogHandle = PlayHandle.Empty;
    }

    private void OnPhaseChanged(int phase)
    {
        if (phase == 2 && !_defeated && Manager != null) Manager.PlayMusicEvent("Music.BossPhase2", gameObject.scene);
    }
    private void OnDamaged(HitData hit)
    {
        if (_defeated || hit.Damage <= 0f) return;
        string eventId = "Boss.Hurt";
        SoundSetSO set = ResolveSet();
        if (_health.LastDamageWasDuringGroggy && set != null &&
            set.TryGetEvent("Boss.GroggyHurt", out EventBinding binding) && binding.Enabled)
        {
            if (!set.TryGetSound(binding.SoundId, out SoundData sound))
                Manager?.Catalog?.TryGetSound(binding.SoundId, out sound);
            if (sound != null && sound.HasValidClip) eventId = "Boss.GroggyHurt";
        }
        PlayResult(eventId, hit.HitPoint);
    }
    private void OnDefeated(Boss boss)
    {
        if (boss != _boss || _defeated) return;
        _defeated = true;
        StopFrogAmbience();
        PlayResult("Boss.Defeat", transform.position);
        if (Manager != null)
        {
            Manager.StopBgm();
            Manager.PlayMusicEvent("Music.Victory", gameObject.scene);
        }
        RetireOwner();
        _patternExecutions.Clear();
        _chargeLoops.Clear();
    }

    private void OnStateChanged(BossState previous, BossState next)
    {
        if (_defeated) return;
        if (next is BossWaitState || next is BossDefeatedState) StopFrogAmbience();
        bool completed = next is BossIdleState && (previous is BossIntroState || previous is BossPhaseTransitionState);
        EndAction(_stateExecution, completed);
        _stateExecution = 0;
        if (next is BossDefeatedState) return;
        _stateExecution = BeginAction();
        if (previous is BossGroggyState && next is BossIdleState) PlayResult("Boss.GroggyRecover", transform.position);
        if (next is BossIntroState)
        {
            PlayAction("Boss.Intro", _stateExecution);
        }
        else if (next is BossPhaseTransitionState)
        {
            PlayAction("Boss.PhaseHowl", _stateExecution);
        }
        else if (next is BossGroggyState)
        {
            PlayResult("Boss.Groggy", transform.position);
        }
        else if (next is BossFinalGroggyState)
        {
            PlayResult("Boss.FinalGroggy", transform.position);
            PlayResult("Boss.Stomp", transform.position);
        }
    }

    private void OnPatternStarted(BossPattern pattern)
    {
        if (_defeated) return;
        if (_patternExecutions.TryGetValue(pattern, out ulong previous)) EndAction(previous, false);
        ulong execution = BeginAction();
        _patternExecutions[pattern] = execution;
        PlayAction("Boss.AttackReady", execution);
        if (pattern is BossSummonPattern) PlayAction("Boss.SummonStart", execution);
    }

    private void OnPatternEnded(BossPattern pattern, bool completed)
    {
        if (!_patternExecutions.TryGetValue(pattern, out ulong execution)) return;
        EndAction(execution, completed);
        _patternExecutions.Remove(pattern);
        _chargeLoops.Remove(pattern);
    }

    private void OnPatternCue(BossPattern pattern, string eventId, Vector3 position, bool result)
    {
        if (result)
        {
            if (eventId == "Boss.ChargeEnd" && _chargeLoops.TryGetValue(pattern, out PlayHandle loop))
            {
                if (Manager != null) Manager.StopHandle(loop);
                _chargeLoops.Remove(pattern);
            }
            PlayResult(eventId, position);
        }
        else if (_patternExecutions.TryGetValue(pattern, out ulong execution))
        {
            PlayHandle handle = PlayAction(eventId, execution, eventId == "Boss.ChargeLoop");
            if (eventId == "Boss.ChargeLoop") _chargeLoops[pattern] = handle;
        }
    }
}
