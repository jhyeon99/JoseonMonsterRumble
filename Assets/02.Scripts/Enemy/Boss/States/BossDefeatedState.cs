using UnityEngine;

/// <summary>
/// 마무리 공격을 맞고 처치.
/// 날려 보내기를 켜 두면 지상 적(LaunchedState)과 같은 방식으로 회전하며 작아지고 흐려지면서 날아감.
/// 끄면 처치 애니메이션 후 일정 시간 뒤 비활성화.
/// 회전 / 축소 기준점: 보스 자식의 LaunchPivot 위치 (없으면 몸 콜라이더 중심)
/// </summary>
public class BossDefeatedState : BossState
{
    // DrawBehind일 때 정렬 순서를 이만큼 뒤로 보냄
    private const int BehindSortingOffset = -100;

    private readonly bool _launch;
    private readonly LaunchSettings _settings;

    private float _timer;
    private bool _isFinished;

    // 날려 보내기
    private float _elapsed;
    private Vector3 _startScale;
    private Quaternion _startRotation;
    private Vector3 _startCenter;
    private Vector3 _centerOffset;
    private Vector3 _currentCenter;
    private float _spinAngle;
    private SpriteRenderer[] _renderers;
    private float[] _startAlphas;

    /// <summary>날리지 않고 처치 애니메이션만</summary>
    public BossDefeatedState(Boss boss)
        : base(boss)
    {
        _launch = false;
    }

    /// <summary>지상 적처럼 날려 보내며 처치</summary>
    public BossDefeatedState(Boss boss, LaunchSettings settings)
        : base(boss)
    {
        _launch = true;
        _settings = settings;
    }

    public override void Enter()
    {
        _timer = _boss.DefeatDuration;
        _isFinished = false;

        _boss.Log(_launch ? "처치 (날려 보내기)" : "처치");
        _boss.ClearMinions();
        _boss.SetTrigger(_boss.DefeatTrigger);
        _boss.Health.HideBar();
        _boss.NotifyDefeated();

        if (_launch)
        {
            BeginLaunch();
        }
    }

    public override void Update()
    {
        if (_isFinished)
        {
            return;
        }

        if (_launch)
        {
            UpdateLaunch();
            return;
        }

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            Finish();
        }
    }

    #region 날려 보내기 (LaunchedState와 같은 계산)

    private void BeginLaunch()
    {
        Transform bossTransform = _boss.transform;

        // 물리를 끄기 전에 기준점을 기록 (LaunchPivot이 있으면 그 위치, 없으면 몸 콜라이더 중심)
        LaunchPivot pivot = _boss.GetComponentInChildren<LaunchPivot>(true);
        _startCenter = pivot != null ? pivot.transform.position : (Vector3)_boss.CenterPosition;
        _startCenter.z = bossTransform.position.z;
        _centerOffset = _startCenter - bossTransform.position;
        _currentCenter = _startCenter;
        _spinAngle = 0f;
        _elapsed = 0f;

        // 물리와 충돌을 끄고 직접 움직임
        Rigidbody2D body = _boss.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
        }

        _startScale = bossTransform.localScale;
        _startRotation = bossTransform.rotation;

        _renderers = _boss.GetComponentsInChildren<SpriteRenderer>();
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

    private void UpdateLaunch()
    {
        _elapsed += Time.deltaTime;
        float duration = Mathf.Max(_settings.Duration, 0.01f);
        float t = Mathf.Clamp01(_elapsed / duration);

        Transform bossTransform = _boss.transform;

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
        bossTransform.rotation = spin * _startRotation;
        bossTransform.localScale = _startScale * scale;
        bossTransform.position = _currentCenter - spin * (_centerOffset * scale);

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

    #endregion

    private void Finish()
    {
        _isFinished = true;
        _boss.NotifyDefeatFinished();

        if (_boss.DisableOnDefeat)
        {
            _boss.gameObject.SetActive(false);
        }
    }
}
