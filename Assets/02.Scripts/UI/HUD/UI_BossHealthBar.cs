using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_BossHealthBar : MonoBehaviour
{
    private const float DefaultDamageDelay = 0.25f;
    private const float DefaultDamageCatchupDuration = 0.35f;
    private const float DefaultHealCatchupDuration = 0.25f;
    private const float DefaultPhaseRefillDuration = 2.0f;
    private const float DefaultFadeDuration = 0.5f;

    private const string NameTextObjectName = "Boss_Name_Text";
    private const string MainFillObjectName = "Bar_Container/Bar_Fill";
    private const string GhostFillObjectName = "Bar_Container/Bar_Ghost";

    [Header("UI 컴포넌트 참조")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TextMeshProUGUI _bossNameText;
    [SerializeField] private Image _mainFill;
    [SerializeField] private Image _ghostFill;

    [Header("색상 설정")]
    [SerializeField] private Color _phase1Color = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color _phase2Color = new Color(0.65f, 0.15f, 0.85f, 1f);
    [SerializeField] private Color _damageGhostColor = Color.white;

    [Header("2페이즈 연출 설정")]
    [SerializeField] private float _phaseRefillDuration = DefaultPhaseRefillDuration;
    [SerializeField] private AnimationCurve _phaseRefillCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("시간 및 보간 설정")]
    [SerializeField] private float _damageDelay = DefaultDamageDelay;
    [SerializeField] private float _damageCatchupDuration = DefaultDamageCatchupDuration;
    [SerializeField] private float _healCatchupDuration = DefaultHealCatchupDuration;

    [Header("현재 상태 모니터링")]
    [SerializeField, ReadOnly] private float _currentRatio = 1f;
    [SerializeField, ReadOnly] private float _ghostRatio = 1f;
    [SerializeField, ReadOnly] private int _currentPhase = 1;
    [SerializeField, ReadOnly] private bool _isPhaseTransitioning;

    private Coroutine _trailCoroutine;
    private Coroutine _refillCoroutine;
    private Coroutine _fadeCoroutine;

    public int CurrentPhase => _currentPhase;
    public bool IsPhaseTransitioning => _isPhaseTransitioning;
    public float CurrentRatio => _currentRatio;

    private void Awake()
    {
        InitializeComponents();
    }

    private void OnDisable()
    {
        StopAllActiveCoroutines();
    }

    public void InitializeComponents()
    {
        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        if (_bossNameText == null)
        {
            Transform nameTransform = transform.Find(NameTextObjectName);
            if (nameTransform != null)
            {
                _bossNameText = nameTransform.GetComponent<TextMeshProUGUI>();
            }
        }

        if (_mainFill == null)
        {
            Transform mainFillTransform = transform.Find(MainFillObjectName);
            if (mainFillTransform != null)
            {
                _mainFill = mainFillTransform.GetComponent<Image>();
            }
        }

        if (_ghostFill == null)
        {
            Transform ghostFillTransform = transform.Find(GhostFillObjectName);
            if (ghostFillTransform != null)
            {
                _ghostFill = ghostFillTransform.GetComponent<Image>();
            }
        }

        if (_phaseRefillCurve == null || _phaseRefillCurve.length == 0)
        {
            _phaseRefillCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }
    }

    public void Initialize(string bossName, float maxHealth)
    {
        StopAllActiveCoroutines();

        _currentPhase = 1;
        _isPhaseTransitioning = false;
        _currentRatio = 1f;
        _ghostRatio = 1f;

        if (_bossNameText != null && !string.IsNullOrEmpty(bossName))
        {
            _bossNameText.text = bossName;
        }

        if (_mainFill != null)
        {
            _mainFill.color = _phase1Color;
            _mainFill.fillAmount = 1f;
        }

        if (_ghostFill != null)
        {
            _ghostFill.color = _damageGhostColor;
            _ghostFill.fillAmount = 1f;
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
        }
    }

    public void UpdateHealth(float currentHealth, float maxHealth, bool isInstant)
    {
        if (_isPhaseTransitioning)
        {
            return;
        }

        float targetRatio = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;

        if (isInstant)
        {
            StopTrailCoroutine();
            _currentRatio = targetRatio;
            _ghostRatio = targetRatio;

            if (_mainFill != null)
            {
                _mainFill.fillAmount = targetRatio;
            }

            if (_ghostFill != null)
            {
                _ghostFill.fillAmount = targetRatio;
            }

            return;
        }

        if (Mathf.Approximately(targetRatio, _currentRatio))
        {
            return;
        }

        if (targetRatio < _currentRatio)
        {
            if (_mainFill != null)
            {
                _mainFill.fillAmount = targetRatio;
            }

            _currentRatio = targetRatio;
            StartTrailCoroutine(ApplyDamageTrailRoutine(targetRatio));
        }
        else
        {
            if (_ghostFill != null)
            {
                _ghostFill.color = _damageGhostColor;
                _ghostFill.fillAmount = targetRatio;
            }

            _ghostRatio = targetRatio;
            StartTrailCoroutine(ApplyHealTrailRoutine(targetRatio));
        }
    }

    public void TakeDamage(float damageAmount, float maxHealth)
    {
        float targetHealth = Mathf.Max(0f, (_currentRatio * maxHealth) - damageAmount);
        UpdateHealth(targetHealth, maxHealth, false);
    }

    public void Heal(float healAmount, float maxHealth)
    {
        float targetHealth = Mathf.Min(maxHealth, (_currentRatio * maxHealth) + healAmount);
        UpdateHealth(targetHealth, maxHealth, false);
    }

    public void StartPhase2(float refillDuration = -1f)
    {
        StopAllActiveCoroutines();

        _currentPhase = 2;
        _isPhaseTransitioning = true;
        _currentRatio = 0f;
        _ghostRatio = 0f;

        if (_mainFill != null)
        {
            _mainFill.color = _phase2Color;
            _mainFill.fillAmount = 0f;
        }

        if (_ghostFill != null)
        {
            _ghostFill.color = _damageGhostColor;
            _ghostFill.fillAmount = 0f;
        }

        float duration = refillDuration > 0f ? refillDuration : _phaseRefillDuration;
        _refillCoroutine = StartCoroutine(PhaseRefillRoutine(duration));
    }

    public void Show(float duration = DefaultFadeDuration)
    {
        StartFade(1f, duration);
    }

    public void Hide(float duration = DefaultFadeDuration)
    {
        StartFade(0f, duration);
    }

    private void StartTrailCoroutine(IEnumerator routine)
    {
        StopTrailCoroutine();
        _trailCoroutine = StartCoroutine(routine);
    }

    private void StopTrailCoroutine()
    {
        if (_trailCoroutine != null)
        {
            StopCoroutine(_trailCoroutine);
            _trailCoroutine = null;
        }
    }

    private IEnumerator ApplyDamageTrailRoutine(float targetRatio)
    {
        if (_ghostFill != null)
        {
            _ghostFill.color = _damageGhostColor;
        }

        yield return new WaitForSecondsRealtime(_damageDelay);

        float startGhost = _ghostFill != null ? _ghostFill.fillAmount : _ghostRatio;
        float elapsed = 0f;

        while (elapsed < _damageCatchupDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _damageCatchupDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            float current = Mathf.Lerp(startGhost, targetRatio, smoothT);

            if (_ghostFill != null)
            {
                _ghostFill.fillAmount = current;
            }

            _ghostRatio = current;
            yield return null;
        }

        if (_ghostFill != null)
        {
            _ghostFill.fillAmount = targetRatio;
        }

        _ghostRatio = targetRatio;
        _trailCoroutine = null;
    }

    private IEnumerator ApplyHealTrailRoutine(float targetRatio)
    {
        float startMain = _mainFill != null ? _mainFill.fillAmount : _currentRatio;
        float elapsed = 0f;

        while (elapsed < _healCatchupDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _healCatchupDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            float current = Mathf.Lerp(startMain, targetRatio, smoothT);

            if (_mainFill != null)
            {
                _mainFill.fillAmount = current;
            }

            _currentRatio = current;
            yield return null;
        }

        if (_mainFill != null)
        {
            _mainFill.fillAmount = targetRatio;
        }

        _currentRatio = targetRatio;

        if (_ghostFill != null)
        {
            _ghostFill.fillAmount = targetRatio;
        }

        _ghostRatio = targetRatio;
        _trailCoroutine = null;
    }

    private IEnumerator PhaseRefillRoutine(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float evaluatedValue = _phaseRefillCurve.Evaluate(progress);

            if (_mainFill != null)
            {
                _mainFill.fillAmount = evaluatedValue;
            }

            if (_ghostFill != null)
            {
                _ghostFill.fillAmount = evaluatedValue;
            }

            _currentRatio = evaluatedValue;
            _ghostRatio = evaluatedValue;
            yield return null;
        }

        if (_mainFill != null)
        {
            _mainFill.fillAmount = 1f;
        }

        if (_ghostFill != null)
        {
            _ghostFill.fillAmount = 1f;
        }

        _currentRatio = 1f;
        _ghostRatio = 1f;
        _isPhaseTransitioning = false;
        _refillCoroutine = null;
    }

    private void StartFade(float targetAlpha, float duration)
    {
        if (_canvasGroup == null)
        {
            return;
        }

        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        _fadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha, duration));
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        if (_canvasGroup == null)
        {
            yield break;
        }

        if (duration <= 0.001f)
        {
            _canvasGroup.alpha = targetAlpha;
            _fadeCoroutine = null;
            yield break;
        }

        float startAlpha = _canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        _canvasGroup.alpha = targetAlpha;
        _fadeCoroutine = null;
    }

    private void StopAllActiveCoroutines()
    {
        StopTrailCoroutine();

        if (_refillCoroutine != null)
        {
            StopCoroutine(_refillCoroutine);
            _refillCoroutine = null;
        }

        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }
    }
}
