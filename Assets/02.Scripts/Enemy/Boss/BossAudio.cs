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
        if (boss == _boss && !_defeated && Manager != null) Manager.PlayMusicEvent("Music.Boss", gameObject.scene);
    }
    private void OnPhaseChanged(int phase)
    {
        if (phase == 2 && !_defeated && Manager != null) Manager.PlayMusicEvent("Music.BossPhase2", gameObject.scene);
    }
    private void OnDamaged(HitData hit) { if (!_defeated && hit.Damage > 0f) PlayResult("Boss.Hurt", hit.HitPoint); }
    private void OnDefeated(Boss boss)
    {
        if (boss != _boss || _defeated) return;
        _defeated = true;
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
        bool completed = next is BossIdleState && (previous is BossIntroState || previous is BossPhaseTransitionState);
        EndAction(_stateExecution, completed);
        _stateExecution = 0;
        if (next is BossDefeatedState) return;
        _stateExecution = BeginAction();
        if (previous is BossGroggyState && next is BossIdleState) PlayResult("Boss.GroggyRecover", transform.position);
        if (next is BossIntroState) PlayAction("Boss.Intro", _stateExecution);
        else if (next is BossPhaseTransitionState) PlayAction("Boss.PhaseHowl", _stateExecution);
        else if (next is BossGroggyState) PlayResult("Boss.Groggy", transform.position);
        else if (next is BossFinalGroggyState) PlayResult("Boss.FinalGroggy", transform.position);
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
            PlayHandle handle = PlayAction(eventId, execution);
            if (eventId == "Boss.ChargeLoop") _chargeLoops[pattern] = handle;
        }
    }
}
