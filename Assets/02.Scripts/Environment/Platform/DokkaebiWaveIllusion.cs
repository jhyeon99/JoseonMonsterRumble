using UnityEngine;

/// <summary>도깨비 웨이브가 생성한 인스턴스에만 붙이는 환영 표현. 공격 소재와 버프 색은 유지한다.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(30)]
public sealed class DokkaebiWaveIllusion : MonoBehaviour
{
    private const int EchoCount = 2;
    private const float SpawnDuration = 0.4f;
    private const float DissolveDuration = 0.45f;
    private const float BodyOpacity = 0.85f;
    private const float EchoOpacity = 0.16f;
    private static readonly Color EchoColor = new Color(0.48f, 0.6f, 0.64f, 1f);

    private EnemyHealth _health;
    private SpriteRenderer _source;
    private readonly SpriteRenderer[] _echoes = new SpriteRenderer[EchoCount];
    private readonly Vector3[] _echoPositions = new Vector3[EchoCount];
    private float _originalAlpha;
    private float _elapsed;
    private float _deathElapsed;
    private bool _isDissolving;
    private bool _isInitialized;

    public void Initialize(Enemy enemy, Material echoMaterial)
    {
        _source = enemy.GetComponent<SpriteRenderer>();
        if (_source == null) _source = enemy.GetComponentInChildren<SpriteRenderer>();
        if (_source == null || echoMaterial == null || enemy.Health == null)
        {
            Debug.LogWarning("DokkaebiWaveIllusion: 몸체 SpriteRenderer, 잔상 Material 또는 EnemyHealth가 없습니다.", this);
            enabled = false;
            return;
        }

        _health = enemy.Health;
        _originalAlpha = _source.color.a;
        for (int index = 0; index < EchoCount; index++)
        {
            var echoObject = new GameObject("IllusionEcho" + index);
            echoObject.layer = _source.gameObject.layer;
            echoObject.transform.SetParent(_source.transform.parent, false);
            SpriteRenderer echo = echoObject.AddComponent<SpriteRenderer>();
            echo.sharedMaterial = echoMaterial;
            echo.color = new Color(EchoColor.r, EchoColor.g, EchoColor.b, 0f);
            _echoes[index] = echo;
            _echoPositions[index] = _source.transform.position;
            CopyEchoPose(echo);
        }
        _health.OnDeath += StartDissolve;
        _isInitialized = true;
        UpdateVisuals(0f);
    }

    private void OnEnable()
    {
        if (!_isInitialized || _health == null) return;
        _health.OnDeath -= StartDissolve;
        _health.OnDeath += StartDissolve;
    }

    private void LateUpdate()
    {
        if (_source == null || PauseController.IsPaused) return;
        UpdateVisuals(Time.deltaTime);
    }

    private void UpdateVisuals(float deltaTime)
    {
        _elapsed += deltaTime;
        if (_isDissolving) _deathElapsed += deltaTime;
        float spawn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_elapsed / SpawnDuration));
        float dissolve = _isDissolving ? Mathf.Clamp01(_deathElapsed / DissolveDuration) : 0f;
        Color bodyColor = _source.color;
        // RGB와 기존 Material은 피격·버프 효과가 계속 관리한다.
        bodyColor.a = _originalAlpha * BodyOpacity * spawn * (1f - dissolve);
        _source.color = bodyColor;

        for (int index = 0; index < EchoCount; index++)
        {
            SpriteRenderer echo = _echoes[index];
            if (echo == null) continue;
            float side = index == 0 ? -1f : 1f;
            if (_isDissolving)
            {
                _echoPositions[index] += new Vector3(side * 0.65f, 0.45f, 0f) * deltaTime;
            }
            else
            {
                CopyEchoPose(echo);
                float follow = 1f - Mathf.Exp(-deltaTime / (0.055f + index * 0.045f));
                _echoPositions[index] = Vector3.Lerp(_echoPositions[index], _source.transform.position, follow);
            }

            float shimmer = Mathf.Sin(_elapsed * 5f + index * Mathf.PI);
            var offset = new Vector3(side * (0.035f + (1f - spawn) * 0.25f), shimmer * 0.025f, 0f);
            echo.transform.position = _echoPositions[index] + offset;
            echo.enabled = _source.enabled && dissolve < 1f;
            Color echoColor = EchoColor;
            echoColor.a = EchoOpacity * (0.8f + shimmer * 0.2f) * (1f - dissolve);
            echo.color = echoColor;
        }
    }

    private void CopyEchoPose(SpriteRenderer echo)
    {
        echo.sprite = _source.sprite;
        echo.flipX = _source.flipX;
        echo.flipY = _source.flipY;
        echo.sortingLayerID = _source.sortingLayerID;
        echo.sortingOrder = _source.sortingOrder - 1;
        echo.transform.rotation = _source.transform.rotation;
        echo.transform.localScale = _source.transform.localScale;
    }

    private void StartDissolve()
    {
        _isDissolving = true;
        _deathElapsed = 0f;
    }

    private void OnDisable()
    {
        if (!_isInitialized) return;
        if (_health != null) _health.OnDeath -= StartDissolve;
        if (_source != null)
        {
            Color color = _source.color;
            color.a = _originalAlpha;
            _source.color = color;
        }
        foreach (SpriteRenderer echo in _echoes)
        {
            if (echo != null) echo.enabled = false;
        }
    }

    private void OnDestroy()
    {
        foreach (SpriteRenderer echo in _echoes)
        {
            if (echo != null) Destroy(echo.gameObject);
        }
    }
}
