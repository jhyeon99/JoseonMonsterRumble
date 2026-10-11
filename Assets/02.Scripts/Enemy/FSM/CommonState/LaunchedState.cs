using UnityEngine;

/// <summary>
/// 마무리 공격에 맞아 멀리 날아가며 사라지는 상태 (사망 처리).
/// 실제로는 조금만 떠오르고, 회전하며 작아지고 흐려지는 것으로 원근감을 표현.
/// 끝나면 소울을 떨어뜨리고 비활성화 (DeadState와 같은 마무리).
/// 회전 / 축소 기준점: 적 프리팹 자식의 LaunchPivot 컴포넌트 위치 (없으면 콜라이더 중심)
/// </summary>
public class LaunchedState : EnemyState
{
    // DrawBehind일 때 정렬 순서를 이만큼 뒤로 보냄
    private const int BehindSortingOffset = -100;

    private readonly Enemy _enemy;
    private readonly LaunchSettings _settings;
    private readonly float _directionX;

    private float _elapsed;
    private Vector3 _startScale;
    private Quaternion _startRotation;
    private bool _isFinished;

    // 회전 / 축소 기준점 (오브젝트 기준점이 발밑이라 그대로 돌리면 발을 축으로 돎)
    private Vector3 _startCenter;
    private Vector3 _centerOffset;
    private Vector3 _currentCenter;
    private float _spinAngle;

    private SpriteRenderer[] _renderers;
    private float[] _startAlphas;

    public LaunchedState(Enemy enemy, LaunchSettings settings, float directionX)
    {
        _enemy = enemy;
        _settings = settings;
        _directionX = directionX >= 0f ? 1f : -1f;
    }

    public override void Enter()
    {
        _elapsed = 0f;
        _isFinished = false;

        // 방향 반전 / 회전 스크립트가 연출을 덮어쓰지 않게 고정
        _enemy.DirectionLock?.LockDirection();
        _enemy.Animation.SetGroggy(false);
        _enemy.Animation.PlayLaunchHit();

        // 물리를 끄기 전에 기준점을 기록 (LaunchPivot이 있으면 그 위치, 없으면 콜라이더 중심)
        Transform enemyTransform = _enemy.transform;
        LaunchPivot pivot = _enemy.GetComponentInChildren<LaunchPivot>(true);
        _startCenter = pivot != null ? pivot.transform.position : _enemy.GetEnemyCenterPosition();
        _startCenter.z = enemyTransform.position.z;
        _centerOffset = _startCenter - enemyTransform.position;
        _currentCenter = _startCenter;
        _spinAngle = 0f;

        if (ItemPool.Instance != null)
        {
            ItemPool.Instance.SpawnDeathSouls(_startCenter, 5);
        }

        // 물리와 충돌을 끄고 직접 움직임
        Rigidbody2D rb = _enemy.EnemyRb;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.simulated = false;

        _startScale = enemyTransform.localScale;
        _startRotation = enemyTransform.rotation;

        _renderers = _enemy.GetComponentsInChildren<SpriteRenderer>();
        _startAlphas = new float[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            _startAlphas[i] = _renderers[i].color.a;

            if (_settings.DrawBehind)
            {
                _renderers[i].sortingOrder += BehindSortingOffset;
            }
        }
    }

    public override void Update()
    {
        if (_isFinished)
        {
            return;
        }

        _elapsed += Time.deltaTime;
        float duration = Mathf.Max(_settings.Duration, 0.01f);
        float t = Mathf.Clamp01(_elapsed / duration);

        Transform enemyTransform = _enemy.transform;

        // 몸 중심의 이동: 높이는 조금만, 옆으로 살짝 밀려남
        float heightRatio = Evaluate(_settings.HeightCurve, t, t);
        _currentCenter = _startCenter + new Vector3(
            _settings.Width * heightRatio,
            _settings.Height * heightRatio,
            0f
        );

        // 회전 속도도 곡선으로 (맞는 순간 빠르게 → 멀어지며 느려짐)
        float spinRatio = Evaluate(_settings.SpinCurve, t, 1f);
        _spinAngle += _settings.MaxSpinSpeed * spinRatio * Time.deltaTime;
        var spin = Quaternion.Euler(0f, 0f, _spinAngle);

        // 작아지는 것으로 멀어지는 느낌
        float scale = Mathf.Max(Evaluate(_settings.ScaleCurve, t, 1f - t), 0f);

        // 몸 중심을 기준으로 회전 / 축소되도록, 중심이 제자리에 오게 오브젝트 위치를 역산
        enemyTransform.rotation = spin * _startRotation;
        enemyTransform.localScale = _startScale * scale;
        enemyTransform.position = _currentCenter - spin * (_centerOffset * scale);

        UpdateFade(t);

        if (t >= 1f)
        {
            Finish();
        }
    }

    private void UpdateFade(float t)
    {
        if (_settings.FadeStart >= 1f)
        {
            return;
        }

        float fade = Mathf.InverseLerp(_settings.FadeStart, 1f, t);
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null)
            {
                continue;
            }

            Color color = _renderers[i].color;
            color.a = _startAlphas[i] * (1f - fade);
            _renderers[i].color = color;
        }
    }

    private static float Evaluate(AnimationCurve curve, float t, float fallback)
    {
        return curve != null && curve.length > 0 ? curve.Evaluate(t) : fallback;
    }

    private void Finish()
    {
        _isFinished = true;
        _enemy.gameObject.SetActive(false);
    }
}
