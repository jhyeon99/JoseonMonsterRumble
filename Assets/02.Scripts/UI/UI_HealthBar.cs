using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UI_HealthBar : MonoBehaviour
{
    [Header("컴포넌트 참조")]
    [SerializeField] private PlayerHealth _playerHealth;
    [SerializeField] private Image _mainFill;
    [SerializeField] private Image _ghostFill;

    [Header("잔상 색상 설정")]
    [SerializeField] private Color _damageGhostColor = new Color(0.9f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color _healGhostColor = new Color(0.2f, 0.85f, 0.3f, 1f);

    [Header("시간 및 보간 설정")]
    [SerializeField] private float _damageDelay = 0.25f;
    [SerializeField] private float _damageCatchupDuration = 0.35f;
    [SerializeField] private float _healCatchupDuration = 0.2f;

    [SerializeField] private float _currentRatio = 1f;
    [SerializeField] private float _ghostRatio = 1f;

    private Coroutine _trailCoroutine;

    private void Awake()
    {
        InitializeComponents();
    }

    private void Start()
    {
        if (_playerHealth == null)
        {
            _playerHealth = FindFirstObjectByType<PlayerHealth>();
        }

        if (_playerHealth != null)
        {
            _playerHealth.OnHealthChanged += HandleHealthChanged;
            UpdateHealth(_playerHealth.CurrentHealth, _playerHealth.MaxHealth, true);
        }
    }

    private void OnEnable()
    {
        if (_playerHealth != null)
        {
            _playerHealth.OnHealthChanged -= HandleHealthChanged;
            _playerHealth.OnHealthChanged += HandleHealthChanged;
        }
    }

    private void OnDisable()
    {
        if (_playerHealth != null)
        {
            _playerHealth.OnHealthChanged -= HandleHealthChanged;
        }

        StopTrailCoroutine();
    }

    private void InitializeComponents()
    {
        if (_mainFill == null)
        {
            Transform mainFillTransform = transform.Find("Bar_Fill");
            if (mainFillTransform != null)
            {
                _mainFill = mainFillTransform.GetComponent<Image>();
            }
        }

        if (_ghostFill == null)
        {
            Transform ghostTransform = transform.Find("Bar_Ghost");
            if (ghostTransform != null)
            {
                _ghostFill = ghostTransform.GetComponent<Image>();
            }
            else if (_mainFill != null)
            {
                CreateGhostFill();
            }
        }
    }

    private void CreateGhostFill()
    {
        GameObject ghostObj = Instantiate(_mainFill.gameObject, transform);
        ghostObj.name = "Bar_Ghost";
        ghostObj.transform.SetSiblingIndex(_mainFill.transform.GetSiblingIndex());

        _ghostFill = ghostObj.GetComponent<Image>();
        _ghostFill.color = _damageGhostColor;
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        UpdateHealth(currentHealth, maxHealth, false);
    }

    public void UpdateHealth(float currentHealth, float maxHealth, bool isInstant)
    {
        float targetRatio = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;

        if (isInstant)
        {
            StopTrailCoroutine();
            _currentRatio = targetRatio;
            _ghostRatio = targetRatio;

            if (_mainFill != null) _mainFill.fillAmount = targetRatio;
            if (_ghostFill != null) _ghostFill.fillAmount = targetRatio;
            return;
        }

        if (Mathf.Approximately(targetRatio, _currentRatio))
        {
            return;
        }

        if (targetRatio < _currentRatio)
        {
            // 데미지 피격
            if (_mainFill != null)
            {
                _mainFill.fillAmount = targetRatio;
            }

            _currentRatio = targetRatio;
            StartTrailCoroutine(ApplyDamageTrail(targetRatio));
        }
        else
        {
            // 체력 회복
            if (_ghostFill != null)
            {
                _ghostFill.color = _healGhostColor;
                _ghostFill.fillAmount = targetRatio;
            }

            _ghostRatio = targetRatio;
            StartTrailCoroutine(ApplyHealTrail(targetRatio));
        }
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

    private IEnumerator ApplyDamageTrail(float targetRatio)
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
            float current = Mathf.Lerp(startGhost, targetRatio, t);

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

    private IEnumerator ApplyHealTrail(float targetRatio)
    {
        float startMain = _mainFill != null ? _mainFill.fillAmount : _currentRatio;
        float elapsed = 0f;

        while (elapsed < _healCatchupDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _healCatchupDuration);
            float current = Mathf.Lerp(startMain, targetRatio, t);

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
}
