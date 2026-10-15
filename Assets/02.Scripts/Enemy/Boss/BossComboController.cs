using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossComboController : MonoBehaviour
{
    [Serializable]
    private sealed class ComboStep
    {
        public BossPattern Pattern;
        public BossPattern Fallback;
    }

    [Serializable]
    private sealed class Combo
    {
        public string Name;
        public BossPattern StartPattern;
        public BossPhaseMask Phases = BossPhaseMask.Both;
        public ComboStep[] Steps;
    }

    [SerializeField, Range(0f, 1f)] private float _comboChance = 0.6f;
    [SerializeField] private Combo[] _combos;
    [SerializeField, ReadOnly] private string _activeComboName;
    [SerializeField, ReadOnly] private int _nextStepIndex;
    private Boss _boss;
    private IReadOnlyList<BossPattern> _patterns;
    private Combo _activeCombo;
    private BossPattern _expectedPattern;
    private bool _hasPendingStep;
    private bool _isSubscribed;

    public bool HasPendingStep => isActiveAndEnabled && _hasPendingStep;

    public void Initialize(Boss boss, IReadOnlyList<BossPattern> patterns)
    {
        UnsubscribeEvents();
        _boss = boss;
        _patterns = patterns;
        if (_combos == null || _combos.Length == 0)
            Debug.LogWarning("BossComboController: 콤보 연결이 없습니다.", this);
        if (isActiveAndEnabled) SubscribeEvents();
    }

    private void OnEnable()
    {
        if (_boss != null) SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        ResetCombo();
    }

    private void SubscribeEvents()
    {
        if (_isSubscribed || _patterns == null) return;
        foreach (BossPattern pattern in _patterns)
            if (pattern != null) pattern.OnExecutionEnded += HandlePatternEnded;
        _boss.OnPhaseChanged += HandlePhaseChanged;
        _isSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!_isSubscribed) return;
        foreach (BossPattern pattern in _patterns)
            if (pattern != null) pattern.OnExecutionEnded -= HandlePatternEnded;
        if (_boss != null) _boss.OnPhaseChanged -= HandlePhaseChanged;
        _isSubscribed = false;
    }

    private void HandlePhaseChanged(int phase) => ResetCombo();

    public void StartCombo(BossPattern selected)
    {
        ResetCombo();
        if (!isActiveAndEnabled || _boss == null || _combos == null) return;
        BossPhaseMask phase = _boss.Phase >= 2 ? BossPhaseMask.Phase2 : BossPhaseMask.Phase1;
        foreach (Combo combo in _combos)
        {
            if (combo == null || combo.StartPattern != selected || (combo.Phases & phase) == 0 || combo.Steps == null || combo.Steps.Length == 0) continue;
            if (UnityEngine.Random.value >= _comboChance) return;
            _activeCombo = combo;
            _activeComboName = combo.Name;
            _expectedPattern = selected;
            return;
        }
    }

    private void HandlePatternEnded(BossPattern pattern, bool completed)
    {
        if (_activeCombo == null || pattern != _expectedPattern) return;
        if (!completed || _nextStepIndex >= _activeCombo.Steps.Length)
        {
            ResetCombo();
            return;
        }
        _hasPendingStep = true;
    }

    public bool TrySelectNextPattern(out BossPattern selected)
    {
        selected = null;
        if (!HasPendingStep) return false;
        if (_activeCombo == null || _activeCombo.Steps == null || _nextStepIndex >= _activeCombo.Steps.Length)
        {
            ResetCombo();
            return false;
        }
        ComboStep step = _activeCombo.Steps[_nextStepIndex];
        if (step != null)
        {
            if (step.Pattern != null && step.Pattern.CanUse()) selected = step.Pattern;
            else if (step.Fallback != null && step.Fallback.CanUse()) selected = step.Fallback;
        }
        if (selected == null)
        {
            ResetCombo();
            return false;
        }
        if (!(selected is BossTailPattern)) _boss.FacePlayer();
        _expectedPattern = selected;
        _nextStepIndex++;
        _hasPendingStep = false;
        return true;
    }

    public void ResetCombo()
    {
        _activeCombo = null;
        _expectedPattern = null;
        _hasPendingStep = false;
        _nextStepIndex = 0;
        _activeComboName = string.Empty;
    }
}
