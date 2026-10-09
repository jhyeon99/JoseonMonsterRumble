using System.Collections;
using UnityEngine;

public class SoulItem : Item
{
    [SerializeField] private float _soulAmount = 0.2f;

    [SerializeField] private float _waitTime = 0.2f;
    [SerializeField] private float _absorbDuration = 0.35f;
    [SerializeField] private float _arcHeight = 0.5f;
    [SerializeField] private float _randomRange = 0.2f;
    [SerializeField] private Vector3 _targetOffset = new Vector3(0f, 0.7f, 0f);

    [Header("시각 연출 컴포넌트")]
    [SerializeField] private TrailRenderer _trailRenderer;
    [SerializeField] private ParticleSystem _headParticle;

    private Transform _playerTransform;
    private bool _isWaiting;
    private bool _isCollected;

    private float _absorbTimer;
    private Vector2 _originPosition;
    private Vector2 _controlPoint;

    private Vector2 _scatterVelocity;
    private float _scatterTimer;
    private float _currentWaitTime;
    private Vector3 _baseScale = Vector3.one;

    private void Awake()
    {
        InitializeRenderers();
        _baseScale = transform.localScale;
    }

    private void InitializeRenderers()
    {
        if (_trailRenderer == null)
        {
            _trailRenderer = GetComponentInChildren<TrailRenderer>(true);
        }

        if (_headParticle == null)
        {
            _headParticle = GetComponentInChildren<ParticleSystem>(true);
        }
    }

    private void Update()
    {
        if (_playerTransform == null)
        {
            return;
        }

        if (_isWaiting)
        {
            if (_scatterVelocity.sqrMagnitude > 0.01f)
            {
                transform.position += (Vector3)(_scatterVelocity * Time.deltaTime);
                _scatterVelocity = Vector2.Lerp(_scatterVelocity, Vector2.zero, Time.deltaTime * 5f);
            }
            return;
        }

        _absorbTimer += Time.deltaTime;
        float bezierTime = Mathf.Clamp01(_absorbTimer / _absorbDuration);

        Vector3 targetPosition = _playerTransform.position + _targetOffset;
        transform.position = CalculateBezier(_originPosition, _controlPoint, targetPosition, bezierTime);

        if (bezierTime >= 1f)
        {
            if (_playerTransform.TryGetComponent(out Player player))
            {
                OnCollect(player);
            }
            gameObject.SetActive(false);
        }
    }

    private IEnumerator WaitRoutine(float duration)
    {
        _isWaiting = true;
        yield return new WaitForSeconds(duration);
        _isWaiting = false;

        _originPosition = transform.position;
        _controlPoint = (Vector2)transform.position + Vector2.up * _arcHeight +
                        (Vector2)Random.insideUnitSphere * _randomRange;
    }

    private Vector3 CalculateBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        return Vector3.Lerp(Vector3.Lerp(p0, p1, t), Vector3.Lerp(p1, p2, t), t);
    }

    public override void OnSpawn()
    {
        EnsurePlayerTransform();
        _isWaiting = true;
        _isCollected = false;
        _absorbTimer = 0f;
        _currentWaitTime = _waitTime;
        _scatterVelocity = Vector2.zero;

        transform.localScale = _baseScale;
        if (_trailRenderer != null)
        {
            _trailRenderer.widthMultiplier = 1f;
        }

        ResetTrail();
        StartCoroutine(WaitRoutine(_currentWaitTime));
    }

    public void Setup(float amount, Color color, float scaleMultiplier, Vector2 initialVelocity, float waitTime = 0.35f)
    {
        InitializeRenderers();
        EnsurePlayerTransform();

        _soulAmount = amount;
        _currentWaitTime = waitTime;
        _scatterVelocity = initialVelocity;
        _absorbTimer = 0f;
        _isWaiting = true;
        _isCollected = false;

        transform.localScale = _baseScale * scaleMultiplier;
        if (_trailRenderer != null)
        {
            _trailRenderer.widthMultiplier = scaleMultiplier;
        }

        ApplyColor(color);
        ResetTrail();

        StopAllCoroutines();
        StartCoroutine(WaitRoutine(_currentWaitTime));
    }

    private void EnsurePlayerTransform()
    {
        if (_playerTransform == null)
        {
            Player player = FindAnyObjectByType<Player>();
            if (player != null)
            {
                _playerTransform = player.transform;
            }
        }
    }

    private void ResetTrail()
    {
        if (_trailRenderer != null)
        {
            _trailRenderer.Clear();
        }
    }

    private void ApplyColor(Color color)
    {
        if (_trailRenderer != null)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(color, 0f), new GradientColorKey(color * 0.8f, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            _trailRenderer.colorGradient = gradient;
        }

        if (_headParticle != null)
        {
            ParticleSystem.MainModule main = _headParticle.main;
            main.startColor = color;
        }
    }

    public override void OnCollect(Player player)
    {
        if (_isCollected) return;
        _isCollected = true;
        player.Soul.AddSoul(_soulAmount);
    }
}
