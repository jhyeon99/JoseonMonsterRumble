using System;
using UnityEngine;

/// <summary>히트스톱 한 번의 설정</summary>
[Serializable]
public struct HitStopSettings
{
    [Tooltip("멈추는 시간 (실제 초). 0이면 사용 안 함")]
    [Min(0f)]
    public float Duration;

    [Tooltip("멈춘 동안의 시간 배율 (0 = 완전 정지, 1 = 평소 속도)")]
    [Range(0f, 1f)]
    public float TimeScale;

    public HitStopSettings(float duration, float timeScale)
    {
        Duration = duration;
        TimeScale = timeScale;
    }
}

/// <summary>
/// 히트스톱. 씬에 하나만 둠 (매니저 오브젝트).
/// - GameEvents의 플레이어 피격 / 패링 / 마무리 공격에 자동으로 반응
/// - 그 외(보스 패턴 등)에서는 HitStop.Play(설정) 또는 HitStop.Play(시간)으로 호출
/// - 멈춘 중에 다시 걸리면 더 늦게 끝나는 시간, 더 느린 배율을 사용 (약한 히트스톱이 강한 것을 끊지 않음)
/// - 일시정지 중에는 timeScale을 건드리지 않음
/// </summary>
public class HitStop : MonoBehaviour
{
    private static HitStop _instance;

    [Header("플레이어 피격")]
    [SerializeField]
    private HitStopSettings _playerHit = new HitStopSettings(0.05f, 0.02f);

    [Tooltip("피격 데미지 1당 추가되는 시간 (초)")]
    [SerializeField, Min(0f)]
    private float _playerHitDamageBonus = 0.005f;

    [Tooltip("플레이어 피격 히트스톱 최대 시간 (초)")]
    [SerializeField, Min(0f)]
    private float _playerHitMaxDuration = 0.2f;

    [Header("적 피격")]
    [SerializeField]
    private HitStopSettings _enemyLightHit = new HitStopSettings(0.035f, 0.08f);

    [SerializeField]
    private HitStopSettings _enemyHeavyHit = new HitStopSettings(0.075f, 0.02f);

    [Header("부적 명중")]
    [SerializeField] private HitStopSettings _talismanHit = new HitStopSettings(0.025f, 0.15f);
    [Tooltip("연속 명중 시 히트스탑 재발동 최소 간격. 실제 시간 기준")]
    [SerializeField, Min(0f)] private float _talismanHitInterval = 0.15f;
    private float _nextTalismanHitTime;

    [Header("패링")]
    [SerializeField]
    private HitStopSettings _parry = new HitStopSettings(0.12f, 0.02f);

    [Header("마무리 공격")]
    [SerializeField]
    private HitStopSettings _finisher = new HitStopSettings(0.32f, 0.02f);

    private bool _isActive;
    private float _endTime;
    private float _currentTimeScale = 1f;

    /// <summary>지금 히트스톱 중인지</summary>
    public static bool IsActive => _instance != null && _instance._isActive;

    private void OnEnable()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning($"{name}: HitStop이 씬에 여러 개 있습니다. 하나만 남겨 주세요.", this);
        }

        _instance = this;

        GameEvents.OnPlayerHit += HandlePlayerHit;
        GameEvents.OnEnemyHit += HandleEnemyHit;
        GameEvents.OnParry += HandleParry;
        GameEvents.OnFinisher += HandleFinisher;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerHit -= HandlePlayerHit;
        GameEvents.OnEnemyHit -= HandleEnemyHit;
        GameEvents.OnParry -= HandleParry;
        GameEvents.OnFinisher -= HandleFinisher;

        StopInternal();

        if (_instance == this)
        {
            _instance = null;
        }
    }

    /// <summary>설정값으로 히트스톱</summary>
    public static void Play(HitStopSettings settings)
    {
        Play(settings.Duration, settings.TimeScale);
    }

    /// <summary>시간(실제 초)과 배율로 히트스톱</summary>
    public static void Play(float duration, float timeScale = 0.02f)
    {
        if (_instance == null)
        {
            return;
        }

        _instance.Begin(duration, timeScale);
    }

    /// <summary>진행 중인 히트스톱을 바로 끝냄</summary>
    public static void Stop()
    {
        if (_instance == null)
        {
            return;
        }

        _instance.StopInternal();
    }

    private void HandlePlayerHit(HitData hitData)
    {
        float duration = _playerHit.Duration + (hitData.Damage * _playerHitDamageBonus);
        Begin(Mathf.Min(duration, _playerHitMaxDuration), _playerHit.TimeScale);
    }

    private void HandleEnemyHit(HitData hitData)
    {
        if (hitData.IsTalisman)
        {
            // 연속 부적은 진행 중인 히트스탑을 연장하거나 강화하지 않는다.
            if (_isActive || PauseController.IsPaused || Time.unscaledTime < _nextTalismanHitTime)
            {
                return;
            }

            _nextTalismanHitTime = Time.unscaledTime + _talismanHitInterval;
            Begin(_talismanHit.Duration, _talismanHit.TimeScale);
            return;
        }

        HitStopSettings settings = hitData.IsHeavyImpact ? _enemyHeavyHit : _enemyLightHit;
        Begin(settings.Duration, settings.TimeScale);
    }

    private void HandleParry(HitData hitData)
    {
        Begin(_parry.Duration, _parry.TimeScale);
    }

    private void HandleFinisher(Vector2 point)
    {
        Begin(_finisher.Duration, _finisher.TimeScale);
    }

    private void Begin(float duration, float timeScale)
    {
        // 일시정지 중에는 timeScale을 건드리지 않음 (일시정지가 우선)
        if (duration <= 0f || PauseController.IsPaused)
        {
            return;
        }

        float endTime = Time.unscaledTime + duration;
        timeScale = Mathf.Clamp01(timeScale);

        if (_isActive)
        {
            // 겹치면 더 강한 쪽 (늦게 끝나는 시간, 느린 배율)
            _endTime = Mathf.Max(_endTime, endTime);
            _currentTimeScale = Mathf.Min(_currentTimeScale, timeScale);
        }
        else
        {
            _isActive = true;
            _endTime = endTime;
            _currentTimeScale = timeScale;
        }

        Time.timeScale = _currentTimeScale;
    }

    private void Update()
    {
        if (!_isActive || PauseController.IsPaused)
        {
            return;
        }

        if (Time.unscaledTime >= _endTime)
        {
            _isActive = false;
            Time.timeScale = 1f;
            return;
        }

        // 일시정지에서 돌아온 경우 등 다른 곳에서 바뀌었으면 다시 맞춤
        if (!Mathf.Approximately(Time.timeScale, _currentTimeScale))
        {
            Time.timeScale = _currentTimeScale;
        }
    }

    private void StopInternal()
    {
        _nextTalismanHitTime = 0f;
        if (!_isActive)
        {
            return;
        }

        _isActive = false;

        if (!PauseController.IsPaused)
        {
            Time.timeScale = 1f;
        }
    }
}
